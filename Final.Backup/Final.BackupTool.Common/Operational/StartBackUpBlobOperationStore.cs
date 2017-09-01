using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Table;
using Final.BackupTool.Common.Entities;

namespace Final.BackupTool.Common.Operational
{
    public class StartBackUpBlobOperationStore
    {
        private readonly StorageConnection _storageConnection = new StorageConnection();

        public StorageOperationEntity GetLastOperation()
        {
            var partitionKey = GetOperationPartitionKey();

            var query = new TableQuery<StorageOperationEntity>()
                .Where(TableQuery.GenerateFilterCondition(OperationalDictionary.PartitionKey, QueryComparisons.Equal,
                    partitionKey));
            var tableClient = _storageConnection.OperationalAccount.CreateCloudTableClient();
            var table = tableClient.GetTableReference(OperationalDictionary.OperationTableName);
            var results = table.ExecuteQuery(query);
            var operation = results.FirstOrDefault();
            return operation;
        }

        public async Task<BlobOperation> StartAsync()
        {
            try
            {
                var now = DateTimeOffset.UtcNow;
                
                var lastOperation = GetLastOperation();

                var operationType = lastOperation == null ? BlobOperationType.Full : BlobOperationType.Incremental;

                var operationEntity = new StorageOperationEntity
                {
                    PartitionKey = GetOperationPartitionKey(),
                    RowKey = GetOperationRowKey(now),
                    SourceAccount = _storageConnection.ProductionStorageAccount.Credentials.AccountName,
                    DestinationAccount = _storageConnection.BackupStorageAccount.Credentials.AccountName,
                    OperationDate = now,
                    StartTime = DateTimeOffset.UtcNow,
                    OperationType = operationType.ToString()
                };

                var operation = new BlobOperation
                {
                    Id = operationEntity.RowKey,
                    OperationType = operationType,
                    LastOperationDate = lastOperation?.OperationDate
                };

                var insertOperation = TableOperation.Insert(operationEntity);
                var tableClient = _storageConnection.OperationalAccount.CreateCloudTableClient();
                var table = tableClient.GetTableReference(OperationalDictionary.OperationTableName);
                await table.ExecuteAsync(insertOperation);

                return operation;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
                throw;
            }
           
        }

        public async Task WriteCopyOutcomeAsync(DateTimeOffset date, CopyStorageOperation[] copies)
        {
            var tableClient = _storageConnection.OperationalAccount.CreateCloudTableClient();
            var table = tableClient.GetTableReference(OperationalDictionary.OperationDetailsTableName);

            var blobOperationEntities = copies.Select(copy => new CopyStorageOperationEntity
            {
                PartitionKey = GetOperationDetailPartitionKey(date),
                RowKey = copy.SourceName.Replace('/', '_'),
                Source = copy.SourceName,
                Status = copy.CopyStatus.ToString(),
                ExtraInformation = copy.ExtraInformation?.ToString()
            }).ToList();

            var entities = blobOperationEntities.GroupBy(c => c.PartitionKey).ToList();

            var logBatchSourceError = new StringBuilder();
            try
            {
                foreach (var entity in entities)
                {
                    var batchOperation = new TableBatchOperation();
                    foreach (var item in entity)
                    {
                        logBatchSourceError.AppendLine($"RowKey: {item.RowKey}, Source: {item.Source}, Extra: {item.ExtraInformation}");
                        batchOperation.InsertOrMerge(item);
                    }
                    await table.ExecuteBatchAsync(batchOperation);
                    logBatchSourceError.Clear();
                }
            }
            catch (Exception e)
            {
                await Console.Error.WriteLineAsync($"Error {e} on Source Batch: {logBatchSourceError}");
            }
        }
        
        public async Task FinishAsync(BlobOperation blobOperation, Summary summary)
        {
            // get the current back up
            var tableClient = _storageConnection.OperationalAccount.CreateCloudTableClient();
            var table = tableClient.GetTableReference(OperationalDictionary.OperationTableName);

            var retrieveOperation = TableOperation.Retrieve<StorageOperationEntity>(
                GetOperationPartitionKey(),
                blobOperation.Id
                );
            var result = await table.ExecuteAsync(retrieveOperation);

            if (result.Result == null)
            {
                await Console.Error.WriteLineAsync("Could not update operation in operational table because it could not be found");
                return;
            }

            var entity = (StorageOperationEntity)result.Result;
            entity.EndTime = DateTimeOffset.UtcNow;
            entity.Copied = summary.Copied;
            entity.Skipped = summary.Skipped;
            entity.Faulted = summary.Faulted;

            var saveOperation = TableOperation.Replace(entity);
            await table.ExecuteAsync(saveOperation);
        }

        private string GetOperationPartitionKey()
        {
            var sourceAccount = _storageConnection.ProductionStorageAccount.Credentials.AccountName;
            var destinationAccount = _storageConnection.BackupStorageAccount.Credentials.AccountName;

            return $"{sourceAccount}_{destinationAccount}";
        }

        private string GetOperationRowKey(DateTimeOffset date)
        {
            return (DateTimeOffset.MaxValue.Ticks - date.Ticks).ToString("d19");
        }

        private string GetOperationDetailPartitionKey(DateTimeOffset date)
        {
            return $"{GetOperationPartitionKey()}_{GetOperationRowKey(date)}";
        }
    }
}
