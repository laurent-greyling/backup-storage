using System;
using System.Configuration;
using System.Linq;
using System.Reflection;
using Final.BackupTool.Common.Operational;
using Microsoft.Azure;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.RetryPolicies;
using NLog;

namespace Final.BackupTool.Common.Strategy
{
    public class InitializeOperation
    {
        private static AzureOperations _azureOperations;
        private static StorageConnection _storageConnection;

        public InitializeOperation()
        {
            if (!string.IsNullOrEmpty(CloudConfigurationManager.GetSetting("ProductionStorageConnectionString")) &&
                !string.IsNullOrEmpty(CloudConfigurationManager.GetSetting("BackupStorageConnectionString")) &&
                !string.IsNullOrEmpty(CloudConfigurationManager.GetSetting("OperationalStorageConnectionString")))
            {
                _storageConnection = new StorageConnection();
            }
            _azureOperations = new AzureOperations();
        }

        public void Execute(ILogger logger)
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
            _azureOperations.CreateProductionBlobClient().DefaultRequestOptions = new BlobRequestOptions
            {
                RetryPolicy = new ExponentialRetry(TimeSpan.FromSeconds(10), 50)
            };
            _azureOperations.CreateBackupBlobClient().DefaultRequestOptions = new BlobRequestOptions
            {
                RetryPolicy = new ExponentialRetry(TimeSpan.FromSeconds(10), 50)
            };
        }

        private static void CreateOperationalLogTables()
        {
            _azureOperations.CreateOperationsTable(OperationalDictionary.OperationTableName);
            _azureOperations.CreateOperationsTable(OperationalDictionary.OperationDetailsTableName);
        }

        private static void BackupStorageLooksLikeProductionStorage()
        {
            var backupBlobClientToVerify = _azureOperations.CreateBackupBlobClient();
            var containers = backupBlobClientToVerify.ListContainers();
            var matchCount = containers.Select(container =>
                container.Name.ToLowerInvariant()).Count(n =>
                n.StartsWith(OperationalDictionary.Wad) ||
                n.StartsWith(OperationalDictionary.Azure) ||
                n.StartsWith(OperationalDictionary.CacheClusterConfigs) ||
                n.Contains(OperationalDictionary.StageArtifacts));

            // Fool proofing
            if (matchCount > 0)
            {
                throw new ConfigurationErrorsException("The configured backup storage looks like a production storage!");
            }
        }
    }
}
