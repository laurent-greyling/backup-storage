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
        private static readonly AzureOperations AzureOperations = new AzureOperations();

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
            AzureOperations.CreateProductionBlobClient.DefaultRequestOptions = new BlobRequestOptions
            {
                RetryPolicy = new ExponentialRetry(TimeSpan.FromSeconds(10), 50)
            };
            AzureOperations.CreateBackupBlobClient.DefaultRequestOptions = new BlobRequestOptions
            {
                RetryPolicy = new ExponentialRetry(TimeSpan.FromSeconds(10), 50)
            };
        }

        private static void CreateOperationalLogTables()
        {
            AzureOperations.CreateOperationsTable(OperationalDictionary.OperationTableName);
            AzureOperations.CreateOperationsTable(OperationalDictionary.OperationDetailsTableName);
        }

        private static void BackupStorageLooksLikeProductionStorage()
        {
            var backupBlobClientToVerify = AzureOperations.CreateBackupBlobClient;
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
