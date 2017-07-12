using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
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
                        return (from tbl in tables
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
        /// <returns></returns>
        public static async Task RestoreTableStorageFromBlobAsync(string tablesToRestore,
            CloudStorageAccount storageAccount,
            CloudStorageAccount destStorageAccount)
        {
            //Specified tables to be restored
            var tables = tablesToRestore.Split(',').ToList();

            if (tables.Count > 0)
            {
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
                        MaxDegreeOfParallelism = 10,
                        BoundedCapacity = 40
                    });

                var fromContainerToBlob = new ActionBlock<CloudBlobContainer>(async cntr =>
                    {
                        var blobItems = cntr.ListBlobs().Cast<CloudBlob>().Where(c=>tables.Any(n=>n==c.Name)).ToList();
                        foreach (var blobItem in blobItems)
                        {
                            var tableClient = destStorageAccount.CreateCloudTableClient();
                            var table = tableClient.GetTableReference(blobItem.Name);

                            table.CreateIfNotExists();

                            await ReadBlobAndInsertIntoTableStorage(blobItem, table);
                        }
                    },
                    new ExecutionDataflowBlockOptions
                    {
                        MaxDegreeOfParallelism = 10,
                        BoundedCapacity = 40
                    });

                fromAccountToContainers.LinkTo(fromContainerToBlob);

                await fromAccountToContainers.SendAsync(storageAccount);

                fromAccountToContainers.Complete();
                await fromAccountToContainers.Completion;
                fromContainerToBlob.Complete();
                await fromContainerToBlob.Completion;
            }
        }

        private static async Task ReadBlobAndInsertIntoTableStorage(CloudBlob blobItem, CloudTable table)
        {
            var batchData = new BatchBlock<TableOperation>(40);
            using (var reader = new StreamReader(blobItem.OpenRead()))
            {
                var backupData = reader.ReadToEnd();
                var restoreTableDataEntities =
                    JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(backupData);

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
                                dynamic dynamicProperty = Convert.ChangeType(row.Value, row.Value.GetType());
                                tableEntity.Properties.Add(row.Key, new EntityProperty(dynamicProperty));
                                break;
                        }
                    }
                    await batchData.SendAsync(TableOperation.InsertOrMerge(tableEntity));
                }

                var copyTables = new ActionBlock<TableOperation[]>(prc =>
                {
                    var batchOp = new TableBatchOperation();

                    foreach (var pr in prc)
                    {
                        batchOp.Add(pr);
                    }

                    table.ExecuteBatch(batchOp);
                }, new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = 10,
                    BoundedCapacity = 40
                });

                batchData.LinkTo(copyTables);

                batchData.Complete();
                await batchData.Completion;
                copyTables.Complete();
                await copyTables.Completion;
            }
        }
    }
}
