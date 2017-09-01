using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks.Dataflow;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;
using Final.BackupTool.Common.Operational;

namespace Final.BackupTool.Common.Blocks
{
    public class RestoreTableBlock
    {
        private static readonly StorageConnection StorageConnection = new StorageConnection();

        public static IPropagatorBlock<CloudBlobContainer, CopyStorageOperation> Create(BlobCommands commands, DateTimeOffset date)
        {
            var retrieveBlobItems = RetrieveBlobItems(commands);
            var restoreTables = RestoreTables(date);

            retrieveBlobItems.LinkTo(restoreTables, new DataflowLinkOptions { PropagateCompletion = true });

            return DataflowBlock.Encapsulate(retrieveBlobItems, restoreTables);
        }

        private static TransformManyBlock<CloudBlobContainer, CloudAppendBlob> RetrieveBlobItems(BlobCommands commands)
        {
            return new TransformManyBlock<CloudBlobContainer, CloudAppendBlob>(container =>
                GetBlobItems(commands, container));
        }

        private static TransformBlock<CloudAppendBlob, CopyStorageOperation> RestoreTables(DateTimeOffset date)
        {
            var operationStore = new StartRestoreTableOperationStore();
            var fromTableItemToStorageOperation =
                new TransformBlock<CloudAppendBlob, CopyStorageOperation>(async table =>
                    {
                        var copyStatus = DeserialiseAndRestoreTable(table);
                        await operationStore.WriteCopyOutcomeAsync(date, copyStatus);
                        return copyStatus;
                    },
                    new ExecutionDataflowBlockOptions
                    {
                        MaxDegreeOfParallelism = 20,
                        BoundedCapacity = 20
                    });
            return fromTableItemToStorageOperation;
        }

        /// <summary>
        /// Get the blob items
        /// </summary>
        /// <param name="commands"></param>
        /// <param name="container"></param>
        /// <returns></returns>
        private static List<CloudAppendBlob> GetBlobItems(BlobCommands commands, CloudBlobContainer container)
        {
            //Specified tables to be restored
            var tables = commands.TableName.Replace(" ", "").Split(',').ToList();

            if (!string.IsNullOrEmpty(commands.FromDate))
            {
                try
                {
                    var from = DateTimeOffset.ParseExact(commands.FromDate, OperationalDictionary.DateFormat, CultureInfo.InvariantCulture);
                    var to = DateTimeOffset.ParseExact(commands.ToDate, OperationalDictionary.DateFormat, CultureInfo.InvariantCulture);

                    var snapShotsItems = container.ListBlobs(blobListingDetails: BlobListingDetails.All, useFlatBlobListing: true)
                        .Cast<CloudAppendBlob>()
                        .Where(c => c.IsSnapshot && c.SnapshotTime.GetValueOrDefault().DateTime >= from.DateTime)
                        .ToList();

                    if (snapShotsItems.Count > 1)
                    {
                        snapShotsItems = snapShotsItems
                            .Where(c => c.SnapshotTime.GetValueOrDefault().DateTime < to.DateTime).ToList();
                    }

                    return tables.Contains("*")
                        ? snapShotsItems
                        : snapShotsItems.Where(c => tables.Any(n => n == c.Name)).ToList();
                }
                catch (Exception e)
                {
                    Console.Error.WriteLineAsync($"Could not get the tables from blob; {e}");
                }
            }

            throw new InvalidOperationException("Set d|date= and e|endDate to a time stamp of 2017-07-15T19:05:46 to restore");
        }

        /// <summary>
        /// Deserialise Json read from blob into a Dynamic table entity and its properties
        /// Then restore the table
        /// </summary>
        /// <param name="blobItem"></param>
        /// <returns></returns>
        private static CopyStorageOperation DeserialiseAndRestoreTable(
            CloudAppendBlob blobItem)
        {
            try
            {
                var destStorageAccount = StorageConnection.ProductionStorageAccount;
                var tableClient = destStorageAccount.CreateCloudTableClient();
                var table = tableClient.GetTableReference(blobItem.Name);

                Console.WriteLine($"Restoring table: {table}");
                table.CreateIfNotExists();
                
                using (var reader = new StreamReader(blobItem.OpenRead()))
                {
                    var entities = new List<DynamicTableEntity>();
                    while (!reader.EndOfStream)
                    {
                        var tableEntity = CreateTableEntity(reader);

                        if (entities.Count > 0 && entities[0].PartitionKey != tableEntity.PartitionKey)
                        {
                            Save(entities, table);
                        }

                        entities.Add(tableEntity);

                        if (entities.Count % 100 == 0)
                        {
                            Save(entities, table);
                        }
                    }

                    if (entities.Count > 0)
                    {
                        Save(entities, table);
                    }

                    Console.WriteLine($"Finished restoring table: {table}");
                    return new CopyStorageOperation
                    {
                        SourceContainerName = OperationalDictionary.TableBackUpContainerName,
                        SourceBlobName = blobItem.Name,
                        SourceTableName = blobItem.Name,
                        CopyStatus = StorageCopyStatus.Completed
                    };
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"Error: Could not deserialise table entity; {e}");

                return new CopyStorageOperation
                {
                    SourceContainerName = OperationalDictionary.TableBackUpContainerName,
                    SourceBlobName = blobItem.Name,
                    SourceTableName = blobItem.Name,
                    CopyStatus = StorageCopyStatus.Faulted,
                    ExtraInformation = e
                };
            }
        }

        private static DynamicTableEntity CreateTableEntity(StreamReader reader)
        {
            var backupData = reader.ReadLine();
            var restoreTableDataEntities =
                JsonConvert.DeserializeObject<Dictionary<string, object>>(backupData);

            var tableEntity = new DynamicTableEntity();

            foreach (var item in restoreTableDataEntities)
            {
                switch (item.Key)
                {
                    case OperationalDictionary.PartitionKey:
                        tableEntity.PartitionKey = (string)item.Value;
                        break;
                    case OperationalDictionary.RowKey:
                        tableEntity.RowKey = (string)item.Value;
                        break;
                    case OperationalDictionary.TimeStamp:
                        tableEntity.Timestamp = DateTimeOffset.Parse((string)item.Value,
                            CultureInfo.CurrentCulture);
                        break;
                    default:
                        //Change the property into the correct type as it was originally saved as
                        dynamic dynamicProperty = Convert.ChangeType(item.Value,
                            item.Value.GetType());
                        tableEntity.Properties.Add(item.Key,
                            new EntityProperty(dynamicProperty));
                        break;
                }
            }
            return tableEntity;
        }

        private static void Save(List<DynamicTableEntity> entities, CloudTable table)
        {
            var batchOperation = new TableBatchOperation();
            foreach (var entity in entities)
            {
                batchOperation.InsertOrMerge(entity);
            }
            table.ExecuteBatch(batchOperation);
            entities.Clear();
        }
    }
}

