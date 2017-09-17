using System;
using System.IO;
using System.Linq;
using System.Text;
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
            var blobClient = CreateBackupBlobClient();
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

        public CloudBlobClient CreateBackupBlobClient()
        {
            return GetBackupStorageAccount().CreateCloudBlobClient();
        }

        public CloudTableClient CreateBackupTableClient()
        {
            return GetBackupStorageAccount().CreateCloudTableClient();
        }

        public CloudStorageAccount GetBackupStorageAccount()
        {
            return CloudStorageAccount.Parse(StorageConnection.BackupStorageConnectionString);
        }

        public string GetBackupAccountName()
        {
            return GetBackupStorageAccount().Credentials.AccountName;
        }

        #endregion

        #region production
        public CloudBlobContainer ProductionContainerReference(string containerName)
        {
            var blobClient = CreateProductionBlobClient();
            blobClient.DefaultRequestOptions.RetryPolicy = new ExponentialRetry(TimeSpan.FromSeconds(5), 5);
            return blobClient.GetContainerReference(containerName);
        }

        public CloudTable ProductionTableReference(string tableName)
        {
            var destStorageAccount = GetProductionStorageAccount();
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

        public CloudBlobClient CreateProductionBlobClient()
        {
            return GetProductionStorageAccount().CreateCloudBlobClient();
        }

        public CloudTableClient CreateProductionTableClient()
        {
            return GetProductionStorageAccount().CreateCloudTableClient();
        }

        public CloudStorageAccount GetProductionStorageAccount()
        {
            return CloudStorageAccount.Parse(StorageConnection.ProductionStorageConnectionString);
        }

        public string GetProductionAccountName()
        {
            return GetProductionStorageAccount().Credentials.AccountName;
        }

        #endregion

        #region operations
        public CloudBlobContainer OperationsContainerReference(string containerName)
        {
            var blobClient = GetOperationsStorageAccount().CreateCloudBlobClient();
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
            var destStorageAccount = GetOperationsStorageAccount();
            var tableClient = destStorageAccount.CreateCloudTableClient();
            return tableClient.GetTableReference(tableName);
        }

        public CloudTable CreateOperationsTable(string tableName)
        {
            var table = OperationsTableReference(tableName);
            table.CreateIfNotExists();
            return table;
        }

        public void DownloadBlob(string containerName, DateTimeOffset? lastModified)
        {
            var container = OperationsContainerReference(containerName);
            var blob = container.ListBlobs().Cast<CloudAppendBlob>().FirstOrDefault(x => x.Properties.LastModified.Value.Date == lastModified.Value.Date);

            var pathUser = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile, Environment.SpecialFolderOption.Create);
            var pathDownload = Path.Combine(pathUser, $@"Downloads\{blob?.Name}");

            using (var fileStream = File.OpenWrite(pathDownload))
            {
                blob?.DownloadToStream(fileStream);
            }
        }

        public string ReadLatestBlob(string containerName)
        {
            var container = OperationsContainerReference(containerName);

            var blob = container.ListBlobs().Cast<CloudAppendBlob>().LastOrDefault();

            var readLine = new StringBuilder();
            using (var reader = new StreamReader(blob.OpenRead()))
            {
                string readBlob;
                while (!string.IsNullOrEmpty(readBlob = reader.ReadLine()))
                {
                    readLine.AppendLine(readBlob);
                }
            }

            return readLine.ToString();
        }

        public string ReadBlob(string containerName, DateTimeOffset? lastModified)
        {
            var container = OperationsContainerReference(containerName);

            var blob = container.ListBlobs().Cast<CloudAppendBlob>().FirstOrDefault(x=>x.Properties.LastModified.Value.Date == lastModified.Value.Date);

            var readLine = new StringBuilder();
            using (var reader = new StreamReader(blob.OpenRead()))
            {
                string readBlob;
                while (!string.IsNullOrEmpty(readBlob = reader.ReadLine()))
                {
                    readLine.Append(readBlob).AppendLine();
                }
            }

            return readLine.ToString();
        }

        public CloudStorageAccount GetOperationsStorageAccount()
        {
            return CloudStorageAccount.Parse(StorageConnection.OperationStorageConnectionString);
        }
        #endregion
    }
}
