using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Table;
using Final.BackupTool.Common.Entities;

namespace Final.BackupTool.Common.Operational
{
    public class StartRestoreTableOperationStore
    {
        private static readonly AzureOperations AzureOperations = new AzureOperations();
        private readonly string _productionAccountName = AzureOperations.GetProductionAccountName;
        private readonly string _backupAccountName = AzureOperations.GetBackupAccountName;

        public StorageOperationEntity GetLastOperation()
        {
            var partitionKey = GetOperationPartitionKey();

            var query = new TableQuery<StorageOperationEntity>()
                .Where(TableQuery.GenerateFilterCondition(OperationalDictionary.PartitionKey, QueryComparisons.Equal,
                    partitionKey));
            var table = AzureOperations.OperationsTableReference(OperationalDictionary.OperationTableName);
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

                var operationEntity = new StorageOperationEntity
                {
                    PartitionKey = GetOperationPartitionKey(),
                    RowKey = GetOperationRowKey(now),
                    SourceAccount = _backupAccountName,
                    DestinationAccount = _productionAccountName,
                    OperationDate = now,
                    StartTime = DateTimeOffset.UtcNow,
                    OperationType = BlobOperationType.Full.ToString()
                };

                var operation = new BlobOperation
                {
                    Id = operationEntity.RowKey,
                    OperationType = BlobOperationType.Full,
                    LastOperationDate = lastOperation?.OperationDate
                };

                var insertOperation = TableOperation.Insert(operationEntity);
                var table = AzureOperations.OperationsTableReference(OperationalDictionary.OperationTableName);
                await table.ExecuteAsync(insertOperation);

                return operation;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
                throw;
            }
        }
        public async Task WriteCopyOutcomeAsync(DateTimeOffset date, CopyStorageOperation copy)
        {
            var table = AzureOperations.OperationsTableReference(OperationalDictionary.OperationTableName);

            var entity = new CopyStorageOperationEntity
            {
                PartitionKey = GetOperationDetailPartitionKey(date),
                RowKey = copy.SourceTableName,
                Status = copy.CopyStatus.ToString(),
                ExtraInformation = copy.ExtraInformation?.ToString()
            };
            var tableInsert = TableOperation.InsertOrMerge(entity);

            await table.ExecuteAsync(tableInsert);
        }
        public async Task FinishAsync(BlobOperation blobOperation, Summary summary)
        {
            // get the current back up
            var table = AzureOperations.OperationsTableReference(OperationalDictionary.OperationTableName);
            var partitionKey = GetOperationPartitionKey();

            var retrieveOperation = TableOperation.Retrieve<StorageOperationEntity>(
                partitionKey,
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
            return $"tables_{_productionAccountName}_{_backupAccountName}";
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
