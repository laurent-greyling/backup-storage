using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;
using Final.BackupTool.Common.Entities;
using Final.BackupTool.Common.Operational;
using Final.BackupTool.Common.Strategy;

namespace Final.BackupTool.Common.Blocks
{
    public class RestoreTableBlock
    {
        public static IPropagatorBlock<CloudBlobContainer, CopyStorageOperation[]> Create(BlobCommands commands, StorageConnection storageConnection, DateTimeOffset date)
        {
            var retrieveBlobItems = RetrieveBlobItems(commands);
            var createTableItems = CreateTableItems();
            var batchTableItem = new BatchBlock<List<TableItem>>(80);
            var restoreTables = RestoreTables(storageConnection, date);
            
            retrieveBlobItems.LinkTo(createTableItems, new DataflowLinkOptions { PropagateCompletion = true });
            createTableItems.LinkTo(batchTableItem, new DataflowLinkOptions { PropagateCompletion = true });
            batchTableItem.LinkTo(restoreTables, new DataflowLinkOptions { PropagateCompletion = true });

            return DataflowBlock.Encapsulate(retrieveBlobItems, restoreTables);
        }


        private static TransformBlock<CloudBlobContainer, List<CloudBlockBlob>> RetrieveBlobItems(BlobCommands commands)
        {
            var fromContainerToBlob = new TransformBlock<CloudBlobContainer, List<CloudBlockBlob>>(container =>
                    GetBlobItems(commands.TableName, commands.FromDate, commands.ToDate, container),
                new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = 40,
                    BoundedCapacity = 40
                }
            );
            return fromContainerToBlob;
        }

        private static TransformBlock<List<CloudBlockBlob>, List<TableItem>> CreateTableItems()
        {
            var fromBlobToTableItem =
                new TransformBlock<List<CloudBlockBlob>, List<TableItem>>(
                    blobitems => blobitems.Select(DeserialiseJsonIntoDynamicTableEntity).ToList(),
                    new ExecutionDataflowBlockOptions
                    {
                        MaxDegreeOfParallelism = 40,
                        BoundedCapacity = 40
                    }
                );
            return fromBlobToTableItem;
        }

        private static TransformBlock<List<TableItem>[], CopyStorageOperation[]> RestoreTables(StorageConnection storageConnection, DateTimeOffset date)
        {
            var operationStore = new StartRestoreTableOperationStore();
            var fromTableItemToStorageOperation =
                new TransformBlock<List<TableItem>[], CopyStorageOperation[]>(tableItems =>
                    {
                        List<CopyStorageOperation> copyStatus = null;
                        foreach (var tableItem in tableItems)
                        {
                            copyStatus = BatchPartitionKeys(storageConnection.ProductionStorageAccount, tableItem);
                        }
                        operationStore.WriteCopyOutcomeAsync(date, copyStatus?.ToArray(), storageConnection);
                        return copyStatus?.ToArray();
                    },
                    new ExecutionDataflowBlockOptions
                    {
                        MaxDegreeOfParallelism = 40,
                        BoundedCapacity = 40
                    });
            return fromTableItemToStorageOperation;
        }

        /// <summary>
        /// Get the blob items
        /// </summary>
        /// <param name="tablesToRestore"></param>
        /// <param name="snapShotTime"></param>
        /// <param name="endSnapShotTime"></param>
        /// <param name="container"></param>
        /// <returns></returns>
        private static List<CloudBlockBlob> GetBlobItems(string tablesToRestore,
           string snapShotTime,
           string endSnapShotTime,
           CloudBlobContainer container)
        {
            //Specified tables to be restored
            var tables = tablesToRestore.Replace(" ", "").Split(',').ToList();

            if (!string.IsNullOrEmpty(snapShotTime))
            {
                try
                {
                    var from = DateTimeOffset.ParseExact(snapShotTime, "yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
                    var to = DateTimeOffset.ParseExact(endSnapShotTime, "yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);

                    var snapShotsItems = container.ListBlobs(blobListingDetails: BlobListingDetails.All, useFlatBlobListing: true)
                            .Cast<CloudBlockBlob>()
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
        /// </summary>
        /// <param name="blobItem"></param>
        /// <returns></returns>
        private static TableItem DeserialiseJsonIntoDynamicTableEntity(CloudBlockBlob blobItem)
        {
            try
            {
                using (var reader = new StreamReader(blobItem.OpenRead()))
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
                                case OperationalDictionary.PartitionKey:
                                    tableEntity.PartitionKey = (string)row.Value;
                                    break;
                                case OperationalDictionary.RowKey:
                                    tableEntity.RowKey = (string)row.Value;
                                    break;
                                case OperationalDictionary.TimeStamp:
                                    tableEntity.Timestamp = DateTimeOffset.Parse((string)row.Value, CultureInfo.CurrentCulture);
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
                        TableName = blobItem.Name,
                        TableEntity = entities.AsEnumerable()
                    };

                    return tableItem;
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"Error: Could not deserialise table entity; {e}");
                throw;
            }
        }

        /// <summary>
        /// Batch partition keys that are the same. This is done in order to run batches of partition keys instead of one at a time
        /// NOTE: If you have a large table with all different partition keys it will be slow and one at a time, this is just to try
        /// and mitigate this fact. The more of your partition keys are the same the faster the restore will be
        /// </summary>
        /// <param name="destStorageAccount"></param>
        /// <param name="tableItem"></param>
        private static List<CopyStorageOperation> BatchPartitionKeys(CloudStorageAccount destStorageAccount,
            IEnumerable<TableItem> tableItem)
        {
            var copyStorageOperation = new List<CopyStorageOperation>();
            foreach (var tbl in tableItem)
            {
                try
                {
                    var tableClient = destStorageAccount.CreateCloudTableClient();
                    var table = tableClient.GetTableReference(tbl.TableName);

                    table.CreateIfNotExists();
                    Console.WriteLine($"Restoring table: {table}");

                    var entities = tbl.TableEntity.GroupBy(partition => partition.PartitionKey).ToList();

                    Parallel.ForEach(entities, entity =>
                    {
                        //chunk this as batch insert fails for anything above 100, annoying as this can slow down the process of restoring
                        Parallel.ForEach(Batch.Chunk(entity, 100), async items =>
                        {
                            try
                            {
                                var batchOperation = new TableBatchOperation();
                                foreach (var item in items)
                                {
                                    batchOperation.InsertOrMerge(item);
                                }
                                await table.ExecuteBatchAsync(batchOperation);
                            }
                            catch (Exception e)
                            {
                                await Console.Error.WriteLineAsync($"Error: Batch execution failed; {e}");
                            }
                        });
                    });

                    copyStorageOperation.Add(new CopyStorageOperation
                    {
                        SourceContainerName = OperationalDictionary.TableBackUpContainerName,
                        SourceBlobName = tbl.TableName,
                        SourceTableName = tbl.TableName,
                        CopyStatus = StorageCopyStatus.Completed
                    });

                }
                catch (Exception e)
                {
                    copyStorageOperation.Add(new CopyStorageOperation
                    {
                        SourceContainerName = OperationalDictionary.TableBackUpContainerName,
                        SourceBlobName = tbl.TableName,
                        SourceTableName = tbl.TableName,
                        CopyStatus = StorageCopyStatus.Faulted,
                        ExtraInformation = e
                    });

                    Console.Error.WriteLineAsync($"Error: Could not batch Partition keys for restoring tables; {e}");
                }
            }

            return copyStorageOperation;
        }
    }
}
