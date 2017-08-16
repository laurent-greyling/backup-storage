using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Table;
using Final.BackupTool.Common.Entities;

namespace Final.BackupTool.Common.Operational
{
    public class StartBackUpBlobOperationStore
    {
        public StorageOperationEntity GetLastOperation(string sourceAccountName, string destinationAccountName,
            StorageConnection storageConnection)
        {
            var partitionKey = GetOperationPartitionKey(storageConnection);

            var query = new TableQuery<StorageOperationEntity>()
                .Where(TableQuery.GenerateFilterCondition(OperationalDictionary.PartitionKey, QueryComparisons.Equal,
                    partitionKey));
            var tableClient = storageConnection.OperationalAccount.CreateCloudTableClient();
            var table = tableClient.GetTableReference(OperationalDictionary.OperationTableName);
            var results = table.ExecuteQuery(query);
            var operation = results.FirstOrDefault();
            return operation;
        }

        public async Task<BlobOperation> StartAsync(StorageConnection storageConnection)
        {
            try
            {
                var now = DateTimeOffset.UtcNow;

                var sourceAccountName = storageConnection.ProductionStorageAccount.Credentials.AccountName;
                var destinationAccountName = storageConnection.BackupStorageAccount.Credentials.AccountName;

                var lastOperation = GetLastOperation(sourceAccountName, destinationAccountName, storageConnection);

                var operationType = lastOperation == null ? BlobOperationType.Full : BlobOperationType.Incremental;

                var operationEntity = new StorageOperationEntity
                {
                    PartitionKey = GetOperationPartitionKey(storageConnection),
                    RowKey = GetOperationRowKey(now),
                    SourceAccount = storageConnection.ProductionStorageAccount.Credentials.AccountName,
                    DestinationAccount = storageConnection.BackupStorageAccount.Credentials.AccountName,
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
                var tableClient = storageConnection.OperationalAccount.CreateCloudTableClient();
                var table = tableClient.GetTableReference(OperationalDictionary.OperationTableName);
                await table.ExecuteAsync(insertOperation);

                return operation;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
           
        }

        public async Task WriteCopyOutcomeAsync(DateTimeOffset date, CopyStorageOperation[] copies, StorageConnection storageConnection)
        {
            var tableClient = storageConnection.OperationalAccount.CreateCloudTableClient();
            var table = tableClient.GetTableReference(OperationalDictionary.OperationDetailsTableName);

            var blobOperationEntities = copies.Select(copy => new CopyStorageOperationEntity
            {
                PartitionKey = GetOperationDetailPartitionKey(storageConnection, date),
                RowKey = copy.SourceName.Replace('/', '_'),
                Source = copy.SourceName,
                Status = copy.CopyStatus.ToString(),
                ExtraInformation = copy.ExtraInformation?.ToString()
            }).ToList();

            var entities = blobOperationEntities.GroupBy(c => c.PartitionKey).ToList();

            foreach (var entity in entities)
            {
                var batchOperation = new TableBatchOperation();
                foreach (var item in entity)
                {
                    batchOperation.Add(
                    TableOperation.Insert(item));
                }
                await table.ExecuteBatchAsync(batchOperation);
            }
        }
        
        public async Task FinishAsync(BlobOperation blobOperation, Summary summary, StorageConnection storageConnection)
        {
            // get the current back up
            var tableClient = storageConnection.OperationalAccount.CreateCloudTableClient();
            var table = tableClient.GetTableReference(OperationalDictionary.OperationTableName);

            var retrieveOperation = TableOperation.Retrieve<StorageOperationEntity>(
                GetOperationPartitionKey(storageConnection),
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

        private string GetOperationPartitionKey(StorageConnection storageConnection)
        {
            var sourceAccount = storageConnection.ProductionStorageAccount;
            var destinationAccount = storageConnection.BackupStorageAccount;

            return $"{sourceAccount.Credentials.AccountName}_{destinationAccount.Credentials.AccountName}";
        }

        private string GetOperationRowKey(DateTimeOffset date)
        {
            return (DateTimeOffset.MaxValue.Ticks - date.Ticks).ToString("d19");
        }

        private string GetOperationDetailPartitionKey(StorageConnection storageConnection, DateTimeOffset date)
        {
            return $"{GetOperationPartitionKey(storageConnection)}_{GetOperationRowKey(date)}";
        }
    }
}
