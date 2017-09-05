using System;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.RetryPolicies;
using Microsoft.WindowsAzure.Storage.Table;

namespace Final.BackupTool.Common.Operational
{
    public class AzureOperations
    {
        private static readonly StorageConnection StorageConnection = new StorageConnection();

        #region backup
        public CloudBlobContainer BackUpContainerReference(string containerName)
        {
            var blobClient = CreateBackupBlobClient;
            blobClient.DefaultRequestOptions.RetryPolicy = new ExponentialRetry(TimeSpan.FromSeconds(5), 5);
            return blobClient.GetContainerReference(containerName);
        }

        public CloudBlockBlob BackupBlockBlobReference(string containerName, string blobName)
        {
            var container = BackUpContainerReference(containerName);
            return container.GetBlockBlobReference(blobName);
        }

        public CloudBlockBlob BackupBlockBlobReference(string containerName, string blobName, DateTimeOffset? snapShot)
        {
            var container = BackUpContainerReference(containerName);
            return container.GetBlockBlobReference(blobName, snapShot);
        }

        public async Task<CloudBlobContainer> CreateBackUpContainerAsync(string containerName)
        {
            var container = BackUpContainerReference(containerName);
            await container.CreateIfNotExistsAsync();
            return container;
        }

        public CloudBlobClient CreateBackupBlobClient => GetBackupStorageAccount.CreateCloudBlobClient();

        public CloudTableClient CreateBackupTableClient => GetBackupStorageAccount.CreateCloudTableClient();

        public CloudStorageAccount GetBackupStorageAccount => CloudStorageAccount.Parse(StorageConnection.BackupStorageConnectionString);

        public string GetBackupAccountName => GetBackupStorageAccount.Credentials.AccountName;
        #endregion

        #region production
        public CloudBlobContainer ProductionContainerReference(string containerName)
        {
            var blobClient = CreateProductionBlobClient;
            blobClient.DefaultRequestOptions.RetryPolicy = new ExponentialRetry(TimeSpan.FromSeconds(5), 5);
            return blobClient.GetContainerReference(containerName);
        }

        public CloudTable ProductionTableReference(string tableName)
        {
            var destStorageAccount = GetProductionStorageAccount;
            var tableClient = destStorageAccount.CreateCloudTableClient();
            return tableClient.GetTableReference(tableName);
        }

        public CloudBlockBlob ProductionBlockBlobReference(string containerName, string blobName)
        {
            var container = ProductionContainerReference(containerName);
            return container.GetBlockBlobReference(blobName);
        }

        public async Task<CloudBlobContainer> CreateProductionContainerAsync(string containerName)
        {
            var container = ProductionContainerReference(containerName);
            await container.CreateIfNotExistsAsync();
            return container;
        }

        public CloudTable CreateProductionTable(string tableName)
        {
            var table = ProductionTableReference(tableName);
            table.CreateIfNotExists();
            return table;
        }

        public CloudBlobClient CreateProductionBlobClient => GetProductionStorageAccount.CreateCloudBlobClient();

        public CloudTableClient CreateProductionTableClient => GetProductionStorageAccount.CreateCloudTableClient();

        public CloudStorageAccount GetProductionStorageAccount => CloudStorageAccount.Parse(StorageConnection.ProductionStorageConnectionString);

        public string GetProductionAccountName => GetProductionStorageAccount.Credentials.AccountName;

        #endregion

        #region operations
        public CloudBlobContainer OperationsContainerReference(string containerName)
        {
            var blobClient = GetOperationsStorageAccount.CreateCloudBlobClient();
            blobClient.DefaultRequestOptions.RetryPolicy = new ExponentialRetry(TimeSpan.FromSeconds(5), 5);
            return blobClient.GetContainerReference(containerName);
        }

        public CloudBlockBlob OperationsBlockBlobReference(string containerName, string blobName)
        {
            var container = OperationsContainerReference(containerName);
            return container.GetBlockBlobReference(blobName);
        }

        public async Task<CloudBlobContainer> CreateOperationsContainerAsync(string containerName)
        {
            var container = OperationsContainerReference(containerName);
            await container.CreateIfNotExistsAsync();
            return container;
        }

        public CloudTable OperationsTableReference(string tableName)
        {
            var destStorageAccount = GetOperationsStorageAccount;
            var tableClient = destStorageAccount.CreateCloudTableClient();
            return tableClient.GetTableReference(tableName);
        }

        public CloudTable CreateOperationsTable(string tableName)
        {
            var table = OperationsTableReference(tableName);
            table.CreateIfNotExists();
            return table;
        }

        public CloudStorageAccount GetOperationsStorageAccount => CloudStorageAccount.Parse(StorageConnection.OperationStorageConnectionString);
        #endregion




    }
}
