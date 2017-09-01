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
using Final.BackupTool.Common.Operational;

namespace Final.BackupTool.Common.Blocks
{
    public class BackupTableBlock
    {
        private static readonly StorageConnection StorageConnection = new StorageConnection();

        public static IPropagatorBlock<CloudTable, CopyStorageOperation> Create(DateTimeOffset date)
        {
            return CreateCopyTables(date);
        }

        private static TransformBlock<CloudTable, CopyStorageOperation> CreateCopyTables(DateTimeOffset date)
        {
            var operationStore = new StartBackupTableOperationStore();
            var copyToDestination = new TransformBlock<CloudTable, CopyStorageOperation>(async cloudTable =>
                {
                    //Create a container to save tables into blob as json
                    var blobClient = StorageConnection.BackupStorageAccount.CreateCloudBlobClient();
                    blobClient.DefaultRequestOptions.RetryPolicy = new ExponentialRetry(TimeSpan.FromSeconds(5), 5);
                    var container = blobClient.GetContainerReference(OperationalDictionary.TableBackupContainer);

                    container.CreateIfNotExists();
                    var status = await CopyTableToBlobDestination(cloudTable, container, date);
                    await operationStore.WriteCopyOutcomeAsync(date, status);

                    return status;
                },
                new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = 20,
                    BoundedCapacity = 20
                });
            return copyToDestination;
        }

        /// <summary>
        /// Copy tables to blob storage
        /// </summary>
        /// <param name="cloudTable"></param>
        /// <param name="container"></param>
        /// <param name="dateOfOperation"></param>
        /// <returns></returns>
        private static async Task<CopyStorageOperation> CopyTableToBlobDestination(CloudTable cloudTable,
            CloudBlobContainer container,
            DateTimeOffset dateOfOperation)
        {
            var query = new TableQuery();
            var tblData = cloudTable.ExecuteQuery(query);
            var destBlob = container.GetAppendBlobReference(cloudTable.Name);

            await destBlob.CreateOrReplaceAsync();

            Console.WriteLine($"Start BackUp Table: {cloudTable.Name}...");
            try
            {
                var count = 0;
                var memoryStream = new MemoryStream();

                foreach (var entity in tblData)
                {
                    ++count;
                    SerializeEntity(entity, memoryStream);

                    if (count % 10000 != 0) continue;

                    memoryStream.Seek(0L, SeekOrigin.Begin);
                    await destBlob.AppendFromStreamAsync(memoryStream);
                    memoryStream.SetLength(0);
                }

                // Flush out any remaining entities
                if (count % 10000 != 0)
                {
                    memoryStream.Seek(0L, SeekOrigin.Begin);
                    await destBlob.AppendFromStreamAsync(memoryStream);
                }

                //Set a snapshot time in metadata as snapshot time stamp can differ over large number of tables
                //With snapshot if you start running the backup at 2017-06-01 11:59:50 your first tables will be on the first and your last tables on the 2nd
                //this will make restoring all tables for a specific timestamp difficult. With metadata set your time stamp for all tables are 2017-06-01 11:59:50
                destBlob.Metadata[OperationalDictionary.BackUpDate] = dateOfOperation.ToString("u");
                destBlob.SetMetadata();
                destBlob.CreateSnapshot();

                Console.WriteLine($"Finished BackedUp Table: {cloudTable.Name}!!!");

                return new CopyStorageOperation
                {
                    SourceContainerName = OperationalDictionary.TableBackUpContainerName,
                    SourceBlobName = cloudTable.Name,
                    SourceTableName = cloudTable.Name,
                    CopyStatus = StorageCopyStatus.Completed
                };
            }
            catch (Exception e)
            {
                await Console.Error.WriteLineAsync($"Error  in Table  {cloudTable.Name}: Something went wrong trying to store table entity into blob; {e}");
                return new CopyStorageOperation
                {
                    SourceContainerName = OperationalDictionary.TableBackUpContainerName,
                    SourceBlobName = cloudTable.Name,
                    SourceTableName = cloudTable.Name,
                    CopyStatus = StorageCopyStatus.Faulted,
                    ExtraInformation = e
                };

            }
        }

        private static void SerializeEntity(DynamicTableEntity entity, Stream destination)
        {
            var dictionary = new Dictionary<string, object>
            {
                {OperationalDictionary.PartitionKey, entity.PartitionKey},
                {OperationalDictionary.RowKey, entity.RowKey},
                {OperationalDictionary.TimeStamp, entity.Timestamp.ToString()}
            };

            //do this as a dynamic table entity property cannot be serialised.
            //flatten the structure
            foreach (var property in entity.Properties)
            {
                dictionary.Add(property.Key, property.Value.PropertyAsObject);
            }
            var line = $"{JsonConvert.SerializeObject(dictionary)}{Environment.NewLine}";
            var bytes = Encoding.UTF8.GetBytes(line);
            destination.Write(bytes, 0, bytes.Length);
        }
    }
}
