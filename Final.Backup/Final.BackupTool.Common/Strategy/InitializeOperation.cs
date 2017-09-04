using System;
using System.Configuration;
using System.Linq;
using System.Reflection;
using Final.BackupTool.Common.Operational;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.RetryPolicies;
using NLog;

namespace Final.BackupTool.Common.Strategy
{
    public class InitializeOperation
    {
        private static readonly StorageConnection StorageConnection = new StorageConnection();

        public void Initialize(ILogger logger)
        {
            // Log version
            logger.Info($"************VERSION {Assembly.GetExecutingAssembly().GetName().Version}*****************");
            logger.Info("Starting backup tool");

            // Read configuration
            logger.Info("Operation Context - reading configuration...");

            BackupStorageLooksLikeProductionStorage();
            CreateOperationalLogTables();

            SetRequestOptions();
        }

        private static void SetRequestOptions()
        {
            StorageConnection.ProductionBlobClient.DefaultRequestOptions = new BlobRequestOptions
            {
                RetryPolicy = new ExponentialRetry(TimeSpan.FromSeconds(10), 50)
            };
            StorageConnection.BackupBlobClient.DefaultRequestOptions = new BlobRequestOptions
            {
                RetryPolicy = new ExponentialRetry(TimeSpan.FromSeconds(10), 50)
            };
        }

        private static void CreateOperationalLogTables()
        {
            var tableClient = StorageConnection.OperationalAccount.CreateCloudTableClient();
            var table = tableClient.GetTableReference(OperationalDictionary.OperationTableName);
            table.CreateIfNotExists();
            table = tableClient.GetTableReference(OperationalDictionary.OperationDetailsTableName);
            table.CreateIfNotExists();
        }

        private static void BackupStorageLooksLikeProductionStorage()
        {
            var backupBlobClientToVerify = StorageConnection.BackupBlobClient;
            var containers = backupBlobClientToVerify.ListContainers();
            var matchCount = containers.Select(container =>
                container.Name.ToLowerInvariant()).Count(n =>
                n.StartsWith(OperationalDictionary.Wad) ||
                n.StartsWith(OperationalDictionary.Azure) ||
                n.StartsWith(OperationalDictionary.Cacheclusterconfigs) ||
                n.Contains(OperationalDictionary.Stageartifacts));

            // Fool proofing
            if (matchCount > 0)
            {
                throw new ConfigurationErrorsException("The configured backup storage looks like a production storage!");
            }
        }
    }
}
