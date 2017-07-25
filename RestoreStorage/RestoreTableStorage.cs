using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using backup_storage.Entity;
using backup_storage.Shared;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.RetryPolicies;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;

namespace backup_storage.RestoreStorage
{
    public class RestoreTableStorage
    {
        /// <summary>
        /// Restore Tables from backup table storage
        /// </summary>
        /// <param name="tablesToRestore"></param>
        /// <param name="storageAccount"></param>
        /// <param name="destStorageAccount"></param>
        /// <returns></returns>
        public static async Task CopyAndRestoreTableStorageAsync(string tablesToRestore,
            CloudStorageAccount storageAccount,
            CloudStorageAccount destStorageAccount)
        {
            //Specified tables to be restored
            var tables = tablesToRestore.Split(',').ToList();

            if (tables.Count > 0)
            {
                var fromAccountToTables = new TransformManyBlock<CloudStorageAccount, CloudTable>(
                    account =>
                    {
                        var tableClient = storageAccount.CreateCloudTableClient();
                        tableClient.DefaultRequestOptions.RetryPolicy = new ExponentialRetry(TimeSpan.FromSeconds(5), 5);

                        //Return only the CloudTables that are specified to be restored as per -t in commandline arguments
                        return tablesToRestore == "all" 
                        ? tableClient.ListTables() 
                        : (from tbl in tables
                            from stbl in tableClient.ListTables()
                            where stbl.Name == tbl
                            select stbl).AsEnumerable();
                    },
                    new ExecutionDataflowBlockOptions
                    {
                        MaxDegreeOfParallelism = 10,
                        BoundedCapacity = 40
                    });
                await BackupAndRestoreTableStorage.BatchAndMoveTables(storageAccount, destStorageAccount,
                    fromAccountToTables);
            }
        }

        /// <summary>
        /// restore table storage from blob
        /// </summary>
        /// <param name="tablesToRestore"></param>
        /// <param name="storageAccount"></param>
        /// <param name="destStorageAccount"></param>
        /// <param name="snapShotTime"></param>
        /// <param name="endSnapShotTime"></param>
        /// <returns></returns>
        public static async Task RestoreTableStorageFromBlobAsync(string tablesToRestore,
            CloudStorageAccount storageAccount,
            CloudStorageAccount destStorageAccount,
            string snapShotTime,
            string endSnapShotTime)
        {
            var fromAccountToContainers = new TransformBlock<CloudStorageAccount, CloudBlobContainer>(
                account =>
                {
                    var blobClient = storageAccount.CreateCloudBlobClient();
                    blobClient.DefaultRequestOptions.RetryPolicy = new ExponentialRetry(TimeSpan.FromSeconds(5), 5);

                    //only return the wanted containers to be backedup as not all system containers need to be backedup
                    return blobClient.GetContainerReference("tablestoragecontainer");
                },
                new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = 20,
                    BoundedCapacity = 40
                });

            var fromContainerToTable = new ActionBlock<CloudBlobContainer>(async cntr =>
                {
                    try
                    {
                        var blobItems = GetBlobItems(tablesToRestore, snapShotTime, endSnapShotTime, cntr);

                        var cloudTableItem = new TransformBlock<CloudBlob, TableItem>(blobitem 
                            => DeserialiseJsonIntoDynamicTableEntity(blobitem),
                            new ExecutionDataflowBlockOptions
                            {
                                MaxDegreeOfParallelism = 20,
                                BoundedCapacity = 40
                            }
                        );

                        var batchBlock = new BatchBlock<TableItem>(40);
                        var cloudTable = new ActionBlock<TableItem[]>(tableItem =>
                        {
                            BatchPartitionKeys(destStorageAccount, tableItem);
                        });

                        foreach (var blobItem in blobItems)
                        {
                            await cloudTableItem.SendAsync(blobItem);
                            cloudTableItem.LinkTo(batchBlock);
                            batchBlock.LinkTo(cloudTable);
                        }

                        cloudTableItem.Complete();
                        await cloudTableItem.Completion;
                        batchBlock.Complete();
                        await batchBlock.Completion;
                        cloudTable.Complete();
                        await cloudTable.Completion;
                    }
                    catch (InvalidOperationException e)
                    {
                        Console.WriteLine(e);
                    }
                    
                },
                new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = 20,
                    BoundedCapacity = 40
                });

            fromAccountToContainers.LinkTo(fromContainerToTable);

            await fromAccountToContainers.SendAsync(storageAccount);

            fromAccountToContainers.Complete();
            await fromAccountToContainers.Completion;
            fromContainerToTable.Complete();
            await fromContainerToTable.Completion;

            ////TODO: Comment this code back in if you think you have tables missing, it will litst the missing tables
            //var destT = destStorageAccount.CreateCloudTableClient();
            //var tablesRestored = destT.ListTables().Select(c => c.Name).ToList();
            //Console.WriteLine(tablesRestored.Count);
            //var rt = storageAccount.CreateCloudBlobClient().GetContainerReference("tablestoragecontainer");
            //var tdf = rt.ListBlobs(useFlatBlobListing: true).ToList().OfType<CloudBlockBlob>().Select(c => c.Name).ToList();

            //var t = tdf.Except(tablesRestored);

            //foreach (var tt in t)
            //{
            //    Console.WriteLine(tt);
            //}
        }

        private static IEnumerable<CloudBlockBlob> GetBlobItems(string tablesToRestore,
            string snapShotTime,
            string endSnapShotTime,
            CloudBlobContainer cntr)
        {
            //Specified tables to be restored
            var tables = tablesToRestore.Replace(" ", "").Split(',').ToList();

            if (!string.IsNullOrEmpty(snapShotTime))
            {
                try
                {
                    var from = DateTimeOffset.ParseExact(snapShotTime, "dd/MM/yyyy hh:mm:ss", CultureInfo.InvariantCulture);
                    var to = DateTimeOffset.ParseExact(endSnapShotTime, "dd/MM/yyyy hh:mm:ss", CultureInfo.InvariantCulture);

                    return tables.Contains("*")
                        ? cntr.ListBlobs(blobListingDetails: BlobListingDetails.Snapshots, useFlatBlobListing: true)
                            .Cast<CloudBlockBlob>()
                            .Where(c => c.IsSnapshot && c.SnapshotTime.GetValueOrDefault().DateTime >= from.DateTime
                                        && c.SnapshotTime.GetValueOrDefault().DateTime <= to.DateTime)
                            .ToList()
                        : cntr.ListBlobs(blobListingDetails: BlobListingDetails.Snapshots, useFlatBlobListing: true)
                            .Cast<CloudBlockBlob>()
                            .Where(c => c.IsSnapshot && c.SnapshotTime.GetValueOrDefault().DateTime >= from.DateTime
                                        && c.SnapshotTime.GetValueOrDefault().DateTime <= to.DateTime &&
                                        tables.Any(n => n == c.Name))
                            .ToList();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }

            throw new InvalidOperationException("Set -m or --snapshot to a time stamp of 07/15/2017 19:05:46 to restore");
        }

        /// <summary>
        /// Batch partition keys that are the same. This is done in order to run batches of partition keys instead of one at a time
        /// NOTE: If you have a large table with all different partition keys it will be slow and one at a time, this is just to try
        /// and mitigate this fact. The more of your partition keys are the same the faster the restore will be
        /// </summary>
        /// <param name="destStorageAccount"></param>
        /// <param name="tableItem"></param>
        private static void BatchPartitionKeys(CloudStorageAccount destStorageAccount, IEnumerable<TableItem> tableItem)
        {
            try
            {
                Parallel.ForEach(tableItem, async tbl =>
                {
                    var tableClient = destStorageAccount.CreateCloudTableClient();
                    var table = tableClient.GetTableReference(tbl.TableName);

                    table.CreateIfNotExists();

                    var entities = tbl.TableEntity.GroupBy(partition => partition.PartitionKey).ToList();

                    foreach (var entity in entities)
                    {
                        var batchOperation = new TableBatchOperation();
                        foreach (var item in entity)
                        {
                            batchOperation.InsertOrMerge(item);
                        }
                        await table.ExecuteBatchAsync(batchOperation);
                    }
                });
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        /// <summary>
        /// Deserialise Json read from blob into a Dynamic table entity and its properties
        /// </summary>
        /// <param name="blobitem"></param>
        /// <returns></returns>
        private static TableItem DeserialiseJsonIntoDynamicTableEntity(CloudBlob blobitem)
        {
            try
            {
                using (var reader = new StreamReader(blobitem.OpenRead()))
                {
                    var backupData = reader.ReadToEnd();
                    var restoreTableDataEntities =
                        JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(backupData);
                    var entities = new List<DynamicTableEntity>();
                    foreach (var entity in restoreTableDataEntities)
                    {
                        var tableEntity = new DynamicTableEntity();
                        foreach (var row in entity)
                        {
                            switch (row.Key)
                            {
                                case "PartitionKey":
                                    tableEntity.PartitionKey = (string)row.Value;
                                    break;
                                case "RowKey":
                                    tableEntity.RowKey = (string)row.Value;
                                    break;
                                case "TimeStamp":
                                    tableEntity.Timestamp = DateTimeOffset.Parse((string) row.Value, CultureInfo.CurrentCulture);
                                    break;
                                default:
                                    //Change the property into the correct type as it was originally saved as
                                    dynamic dynamicProperty = Convert.ChangeType(row.Value,
                                        row.Value.GetType());
                                    tableEntity.Properties.Add(row.Key,
                                        new EntityProperty(dynamicProperty));
                                    break;
                            }
                        }

                        entities.Add(tableEntity);
                    }

                    var tableItem = new TableItem
                    {
                        TableName = blobitem.Name,
                        TableEntity = entities.AsEnumerable()
                    };

                    return tableItem;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }
    }
}
