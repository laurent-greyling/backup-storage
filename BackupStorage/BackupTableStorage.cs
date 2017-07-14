using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using backup_storage.Entity;
using backup_storage.Shared;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.RetryPolicies;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;

namespace backup_storage.BackupStorage
{
    public class BackupTableStorage
    {
        /// <summary>
        /// Copy and backup table storage one at a time in parallel
        /// </summary>
        /// <param name="storageAccount"></param>
        /// <param name="destStorageAccount"></param>
        public static void CopyTableStorage(CloudStorageAccount storageAccount, CloudStorageAccount destStorageAccount)
        {
            var tableClient = storageAccount.CreateCloudTableClient();
            var tables = tableClient.ListTables().Where(c =>
            {
                var acceptedContainer = c.Name.ToLowerInvariant();
                return !acceptedContainer.StartsWith("wad".ToLowerInvariant()) &&
                       !acceptedContainer.StartsWith("waw".ToLowerInvariant());
            });

            Parallel.ForEach(tables, (table) =>
            {
                var query = new TableQuery();
                var tblData = table.ExecuteQuery(query);

                var tableClientDest = destStorageAccount.CreateCloudTableClient();
                var tbl = tableClientDest.GetTableReference(table.Name);
                tbl.CreateIfNotExists();

                Parallel.ForEach(tblData, async dtaEntity =>
                {
                    var insertDta = TableOperation.InsertOrMerge(dtaEntity);
                    await tbl.ExecuteAsync(insertDta);
                });
            });
        }

        /// <summary>
        /// Copy and backup with dataflow - fastest option to copy tables over
        /// </summary>
        /// <param name="storageAccount"></param>
        /// <param name="destStorageAccount"></param>
        /// <returns></returns>
        public static async Task CopyAndBackUpTableStorageAsync(CloudStorageAccount storageAccount,
            CloudStorageAccount destStorageAccount)
        {
            var fromAccountToTables = new TransformManyBlock<CloudStorageAccount, CloudTable>(
                account =>
                {
                    var tableClient = storageAccount.CreateCloudTableClient();
                    tableClient.DefaultRequestOptions.RetryPolicy = new ExponentialRetry(TimeSpan.FromSeconds(5), 5);

                    return tableClient.ListTables().Where(c =>
                    {
                        var acceptedContainer = c.Name.ToLowerInvariant();
                        return !acceptedContainer.StartsWith("wad".ToLowerInvariant()) &&
                               !acceptedContainer.StartsWith("waw".ToLowerInvariant());
                    });
                },
                new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = 10,
                    BoundedCapacity = 40
                });

            await BackupAndRestoreTableStorage.BatchAndMoveTables(storageAccount, destStorageAccount,
                fromAccountToTables);
        }

        /// <summary>
        /// Copy and backup table storage into blob
        /// </summary>
        /// <param name="storageAccount"></param>
        /// <param name="destStorageAccount"></param>
        /// <returns></returns>
        public static async Task CopyTableStorageIntoBlobAsync(CloudStorageAccount storageAccount,
            CloudStorageAccount destStorageAccount)
        {
            const string tableStorageContainer = "tablestoragecontainer";

            var fromAccountToTables = new TransformManyBlock<CloudStorageAccount, CloudTable>(
                account =>
                {
                    var tableClient = storageAccount.CreateCloudTableClient();
                    tableClient.DefaultRequestOptions.RetryPolicy = new ExponentialRetry(TimeSpan.FromSeconds(5), 5);

                    return tableClient.ListTables().Where(c =>
                    {
                        var acceptedContainer = c.Name.ToLowerInvariant();
                        return !acceptedContainer.StartsWith("wad".ToLowerInvariant()) &&
                               !acceptedContainer.StartsWith("waw".ToLowerInvariant());
                    });
                },
                new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = 10,
                    BoundedCapacity = 40
                });

            var batchTables = new ActionBlock<CloudTable>(
                async tbl =>
                {
                    var query = new TableQuery();
                    var tblData = tbl.ExecuteQuery(query);

                    //Create a container to save tables into blob as json
                    var blobClient = destStorageAccount.CreateCloudBlobClient();
                    blobClient.DefaultRequestOptions.RetryPolicy = new ExponentialRetry(TimeSpan.FromSeconds(5), 5);
                    var container = blobClient.GetContainerReference(tableStorageContainer);
                    container.CreateIfNotExists();

                    var batchData = new BatchBlock<BlobItem>(40);

                    await SerialiseAndAddEntityToBatchAsync(tblData, batchData, tbl);

                    //Copy the json structure of table storage into blob
                    var copyToDestination = new ActionBlock<BlobItem[]>(bli =>
                        {
                            Parallel.ForEach(bli, blobItem =>
                            {
                                var destBlob = container.GetBlockBlobReference(blobItem.BlobName);
                                using (var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(blobItem.Blob)))
                                {
                                    destBlob.UploadFromStream(memoryStream);
                                }
                            });
                        });

                    batchData.LinkTo(copyToDestination);

                    batchData.Complete();
                    await batchData.Completion;
                    copyToDestination.Complete();
                    await copyToDestination.Completion;
                    
                });

            fromAccountToTables.LinkTo(batchTables);

            await fromAccountToTables.SendAsync(storageAccount);

            fromAccountToTables.Complete();
            await fromAccountToTables.Completion;
            batchTables.Complete();
            await batchTables.Completion;
        }

        /// <summary>
        /// Add table entities into a list of dictionary in order to send to batchblock  
        /// </summary>
        /// <param name="tblData"></param>
        /// <param name="batchData"></param>
        /// <param name="tbl"></param>
        /// <returns></returns>
        private static async Task SerialiseAndAddEntityToBatchAsync(IEnumerable<DynamicTableEntity> tblData, 
            BatchBlock<BlobItem> batchData, CloudTable tbl)
        {
            var dictionairyListOfEntities = new List<Dictionary<string, object>>();
            foreach (var dtaEntity in tblData)
            {
                var dictionary = new Dictionary<string, object>
                {
                    {"PartitionKey", dtaEntity.PartitionKey},
                    {"RowKey", dtaEntity.RowKey},
                    {"TimeStamp", dtaEntity.Timestamp.ToString()}
                };

                //do this as a dynamic table entity property cannot be serialised.
                //flatten the structure
                foreach (var prop in dtaEntity.Properties)
                {
                    dictionary.Add(prop.Key, prop.Value.PropertyAsObject);
                }

                dictionairyListOfEntities.Add(dictionary);
            }

            //Serialise to json, so we will save this into blob as json as backup
            await batchData.SendAsync(new BlobItem
            {
                BlobName = tbl.Name,
                Blob = JsonConvert.SerializeObject(dictionairyListOfEntities)
            });
        }
    }
}