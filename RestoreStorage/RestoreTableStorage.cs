using System;
using System.Collections.Generic;
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
        /// <returns></returns>
        public static async Task RestoreTableStorageFromBlobAsync(string tablesToRestore,
            CloudStorageAccount storageAccount,
            CloudStorageAccount destStorageAccount,
            string snapShotTime)
        {
            //Specified tables to be restored
            var tables = tablesToRestore.Split(',').ToList();

            var fromAccountToContainers = new TransformManyBlock<CloudStorageAccount, CloudBlobContainer>(
                account =>
                {
                    var blobClient = storageAccount.CreateCloudBlobClient();
                    blobClient.DefaultRequestOptions.RetryPolicy = new ExponentialRetry(TimeSpan.FromSeconds(5), 5);

                    //only return the wanted containers to be backedup as not all system containers need to be backedup
                    return blobClient.ListContainers().Where(c =>
                    {
                        var acceptedContainer = c.Name.ToLowerInvariant();
                        return acceptedContainer.Contains("tablestoragecontainer");
                    });
                },
                new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = 20,
                    BoundedCapacity = 40
                });

            var fromContainerToTable = new ActionBlock<CloudBlobContainer>(async cntr =>
                {
                    var blobItems = GetBlobItems(tablesToRestore, snapShotTime, cntr, tables);

                    var cloudTableItem = new TransformBlock<CloudBlob, TableItem>(bi => DeserialiseJsonIntoDynamicTableEntity(bi),
                        new ExecutionDataflowBlockOptions
                        {
                            MaxDegreeOfParallelism = 20,
                            BoundedCapacity = 40
                        }
                    );

                    var batchBlock = new BatchBlock<TableItem>(40);
                    var cloudTable = new ActionBlock<TableItem[]>(tl =>
                        {
                            BatchPartitionKeys(destStorageAccount, tl);
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
            CloudBlobContainer cntr,
            List<string> tables)
        {
            IEnumerable<CloudBlockBlob> blobItems;
            if (string.IsNullOrEmpty(snapShotTime))
            {
                blobItems = tablesToRestore == "all"
                    ? cntr.ListBlobs().Cast<CloudBlockBlob>().ToList()
                    : cntr.ListBlobs().Cast<CloudBlockBlob>().Where(c => tables.Any(n => n == c.Name)).ToList();
            }
            else
            {
                //Restore on time put into the metadata as snapshot time can differ in the same date. e.g. 07/15/2017 19:05:46 for first table and 07/15/2017 19:05:50 for last table
                //With the metadata one can set the date at start of process and be sure all tables have the exact same time stamp
                blobItems = tablesToRestore == "all"
                    ? cntr.ListBlobs(blobListingDetails: BlobListingDetails.All, useFlatBlobListing: true)
                        .Cast<CloudBlockBlob>()
                        .Where(c => c.SnapshotTime != null && c.Metadata.Values.Contains(snapShotTime))
                        .ToList()
                    : cntr.ListBlobs(blobListingDetails: BlobListingDetails.All, useFlatBlobListing: true)
                        .Cast<CloudBlockBlob>()
                        .Where(c => c.SnapshotTime != null && c.Metadata.Values.Contains(snapShotTime) &&
                                    tables.Any(n => n == c.Name))
                        .ToList();
            }
            return blobItems;
        }

        /// <summary>
        /// Batch partition keys that are the same. This is done in order to run batches of partition keys instead of one at a time
        /// NOTE: If you have a large table with all different partition keys it will be slow and one at a time, this is just to try
        /// and mitigate this fact. The more of your partition keys are the same the faster the restore will be
        /// </summary>
        /// <param name="destStorageAccount"></param>
        /// <param name="tl"></param>
        private static void BatchPartitionKeys(CloudStorageAccount destStorageAccount, IEnumerable<TableItem> tl)
        {
            Parallel.ForEach(tl, async tbl =>
            {
                var tableClient = destStorageAccount.CreateCloudTableClient();
                var table = tableClient.GetTableReference(tbl.TableName);

                table.CreateIfNotExists();
                var masterList = new List<List<DynamicTableEntity>>();

                var entities = tbl.TableEntity.ToList();

                while (entities.Count > 0)
                {
                    // grab all items with the PartitionKey of the first one in the list
                    var listThisPartitionKey = (from item in entities
                        where item.PartitionKey == entities[0].PartitionKey
                        select item).ToList();

                    // add that list into masterlist
                    masterList.Add(listThisPartitionKey);

                    // now grab everything else that didn't have the first PartitionKey
                    entities = entities.Where(x => x.PartitionKey != entities[0].PartitionKey).ToList();
                }

                // Create the batch operation
                foreach (var list in masterList)
                {
                    while (list.Count > 0)
                    {
                        var batchOperation = new TableBatchOperation();

                        if (list.Count <= 100)
                        {
                            foreach (var entity in list)
                            {
                                batchOperation.InsertOrMerge(entity);
                            }

                            await table.ExecuteBatchAsync(batchOperation);

                            list.RemoveRange(0, list.Count);
                        }
                        else
                        {
                            for (var i = 0; i < 100; i++)
                                batchOperation.InsertOrMerge(list[i]);

                            // execute batch operation
                            await table.ExecuteBatchAsync(batchOperation);

                            //remove those from list
                            list.RemoveRange(0, 100);
                        }
                    }
                }
            });
        }

        /// <summary>
        /// Deserialise Json read from blob into a Dynamic table entity and its properties
        /// </summary>
        /// <param name="bi"></param>
        /// <returns></returns>
        private static TableItem DeserialiseJsonIntoDynamicTableEntity(CloudBlob bi)
        {
            using (var reader = new StreamReader(bi.OpenRead()))
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
                                tableEntity.PartitionKey = (string) row.Value;
                                break;
                            case "RowKey":
                                tableEntity.RowKey = (string) row.Value;
                                break;
                            case "TimeStamp":
                                tableEntity.Timestamp = DateTimeOffset.Parse((string) row.Value);
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
                    TableName = bi.Name,
                    TableEntity = entities.AsEnumerable()
                };

                return tableItem;
            }
        }
    }
}
