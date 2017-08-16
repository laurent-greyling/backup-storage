using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.RetryPolicies;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;
using Final.BackupTool.Common.Entities;
using Final.BackupTool.Common.Operational;

namespace Final.BackupTool.Common.Blocks
{
    public class BackupTableBlock
    {
        public static IPropagatorBlock<CloudTable, CopyStorageOperation[]> Create(StorageConnection storageConnection,
            DateTimeOffset date)
        {
            var createBatch = CreateBatch();
            var batchData = new BatchBlock<BlobItem>(40);
            var copyTables = CreateCopyTables(storageConnection, date);

            createBatch.LinkTo(batchData, new DataflowLinkOptions { PropagateCompletion = true });
            batchData.LinkTo(copyTables, new DataflowLinkOptions {PropagateCompletion = true});

            return DataflowBlock.Encapsulate(createBatch, copyTables);
        }

        private static TransformBlock<CloudTable, BlobItem> CreateBatch()
        {
            return new TransformBlock<CloudTable, BlobItem>(
                tbl => SerialiseAndAddEntityToBatch(tbl),
                new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = 20,
                    BoundedCapacity = 20,
                    EnsureOrdered = false
                }
            );
        }

        private static TransformBlock<BlobItem[], CopyStorageOperation[]> CreateCopyTables(StorageConnection storageConnection, DateTimeOffset date)
        {
            var operationStore = new StartBackupTableOperationStore();
            var copyToDestination = new TransformBlock<BlobItem[], CopyStorageOperation[]>(async blobItems =>
                {
                    //Create a container to save tables into blob as json
                    var blobClient = storageConnection.BackupStorageAccount.CreateCloudBlobClient();
                    blobClient.DefaultRequestOptions.RetryPolicy = new ExponentialRetry(TimeSpan.FromSeconds(5), 5);
                    var container = blobClient.GetContainerReference(OperationalDictionary.TableBackupContainer);

                    container.CreateIfNotExists();

                    var copyStatus = await CopyTableToBlobDestination(blobItems, container, date);
                    await operationStore.WriteCopyOutcomeAsync(date, copyStatus.ToArray(), storageConnection);
                    return copyStatus.ToArray();
                },
                new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = 20,
                    BoundedCapacity = 20,
                    EnsureOrdered = false
                });
            return copyToDestination;
        }

        /// <summary>
        /// Copy tables to blob storage
        /// </summary>
        /// <param name="blobItems"></param>
        /// <param name="container"></param>
        /// <param name="dateOfOperation"></param>
        /// <returns></returns>
        private static async Task<List<CopyStorageOperation>> CopyTableToBlobDestination(BlobItem[] blobItems,
            CloudBlobContainer container,
            DateTimeOffset dateOfOperation)
        {
            var copyStorageOperation = new List<CopyStorageOperation>();
            foreach (var blobItem in blobItems)
            {
                try
                {
                    var destBlob = container.GetBlockBlobReference(blobItem.BlobName);

                    using (var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(blobItem.Blob)))
                    {
                        await destBlob.UploadFromStreamAsync(memoryStream);
                    }

                    //Set a snapshot time in metadata as snapshot time stamp can differ over large number of tables
                    //With snapshot if you start running the backup at 2017-06-01 11:59:50 your first tables will be on the first and your last tables on the 2nd
                    //this will make restoring all tables for a specific timestamp difficult. With metadata set your time stamp for all tables are 2017-06-01 11:59:50
                    destBlob.Metadata[OperationalDictionary.BackUpDate] = dateOfOperation.ToString("u");
                    destBlob.SetMetadata();
                    destBlob.CreateSnapshot();

                    Console.WriteLine(blobItem.BlobName);
                    copyStorageOperation.Add(new CopyStorageOperation
                    {
                        SourceContainerName = OperationalDictionary.TableBackUpContainerName,
                        SourceBlobName = blobItem.BlobName,
                        SourceTableName = blobItem.BlobName,
                        CopyStatus = StorageCopyStatus.Completed
                    });

                }
                catch (Exception e)
                {
                    copyStorageOperation.Add(new CopyStorageOperation
                    {
                        SourceContainerName = OperationalDictionary.TableBackUpContainerName,
                        SourceBlobName = blobItem.BlobName,
                        SourceTableName = blobItem.BlobName,
                        CopyStatus = StorageCopyStatus.Faulted,
                        ExtraInformation = e
                    });
                    await Console.Error.WriteLineAsync($"Error: Something went wrong trying to store table entity into blob; {e}");
                }
            }

            return copyStorageOperation;
        }

        private static BlobItem SerialiseAndAddEntityToBatch(CloudTable tbl)
        {
            var query = new TableQuery();
            var tblData = tbl.ExecuteQuery(query);

            var dictionairyListOfEntities = new List<Dictionary<string, object>>();
            foreach (var dtaEntity in tblData)
            {
                var dictionary = new Dictionary<string, object>
                {
                    {OperationalDictionary.PartitionKey, dtaEntity.PartitionKey},
                    {OperationalDictionary.RowKey, dtaEntity.RowKey},
                    {OperationalDictionary.TimeStamp, dtaEntity.Timestamp.ToString()}
                };

                //do this as a dynamic table entity property cannot be serialised.
                //flatten the structure
                foreach (var property in dtaEntity.Properties)
                {
                    dictionary.Add(property.Key, property.Value.PropertyAsObject);
                }

                dictionairyListOfEntities.Add(dictionary);
            }

            //Serialise to json, so we will save this into blob as json as backup
            return new BlobItem
            {
                BlobName = tbl.Name,
                Blob = JsonConvert.SerializeObject(dictionairyListOfEntities)
            };
        }
    }
}
