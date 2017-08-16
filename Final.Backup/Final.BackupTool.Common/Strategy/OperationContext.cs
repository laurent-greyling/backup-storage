using System;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.RetryPolicies;
using Final.BackupTool.Common.ConsoleCommand;
using Final.BackupTool.Common.Initialization;
using Final.BackupTool.Common.Operational;
using NLog;

namespace Final.BackupTool.Common.Strategy
{
    public class OperationContext : IOperationContext
    {
        
        public int DaysRetentionAfterDelete { get; }
        public StorageConnection StorageConnection =new StorageConnection();
        public ILogger Logger;
        
        public OperationContext(ILogger logger)
        {
            Logger = logger;
            // Log version
            Logger.Info($"************VERSION {Assembly.GetExecutingAssembly().GetName().Version}*****************");
            logger.Info("Starting backup tool");

            // Read configuration
            logger.Info("Operation Context - reading configuration...");
            
            // Fool proofing
            if (BackupStorageLooksLikeProductionStorage(StorageConnection.BackupBlobClient))
            {
                throw new ConfigurationErrorsException("The configured backup storage looks like a production storage!");
            }

            var tableClient = StorageConnection.OperationalAccount.CreateCloudTableClient();
            var table = tableClient.GetTableReference(OperationalDictionary.OperationTableName);
            table.CreateIfNotExists();
            table = tableClient.GetTableReference(OperationalDictionary.OperationDetailsTableName);
            table.CreateIfNotExists();

            // Retrieve other config options
            var daysRetentionAfterDelete = CloudConfigurationManager.GetSetting("DaysRetentionAfterDelete");
            DaysRetentionAfterDelete = int.TryParse(daysRetentionAfterDelete, out int daysRetention) ? daysRetention : 60;

            // Set request options
            StorageConnection.ProductionBlobClient.DefaultRequestOptions = new BlobRequestOptions
            {
                RetryPolicy = new ExponentialRetry(TimeSpan.FromSeconds(10), 50)
            };
            StorageConnection.BackupBlobClient.DefaultRequestOptions = new BlobRequestOptions
            {
                RetryPolicy = new ExponentialRetry(TimeSpan.FromSeconds(10), 50)
            };
        }

        private static bool BackupStorageLooksLikeProductionStorage(CloudBlobClient backupBlobClientToVerify)
        {
            var containers = backupBlobClientToVerify.ListContainers();
            var matchCount = containers.Select(container =>
                container.Name.ToLowerInvariant()).Count(n => n.StartsWith("wad") || n.StartsWith("azure") || n.StartsWith("cacheclusterconfigs") || n.Contains("stageartifacts"));
            return matchCount > 0;
        }
        

        public async Task BackupAsync()
        {
            var storageConnection = new StorageConnection();
            // Both these commands are already heavily parallel, so we just run them in order here
            // so they don't fight over bandwidth amongst themselves
            var tableOperation = Bootstrap.Container.GetInstance<ITableOperation>();
            var tableBackUpFromDate = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
            await tableOperation.BackupAsync(storageConnection);
            var tableBackUpToDate = DateTimeOffset.UtcNow.AddSeconds(3).ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
            
            var blobOperation = Bootstrap.Container.GetInstance<IBlobOperation>();
            var blobBackUpFromDate = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
            await blobOperation.BackupAsync(storageConnection);
            var blobBackUpToDate = DateTimeOffset.UtcNow.AddSeconds(3).ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);

            Logger.Info("===>USE FOR RESTORING<===");
            Logger.Info($"restore-table -t=\"*\" -d=\"{tableBackUpFromDate}\" -e=\"{tableBackUpToDate}\"");
            Logger.Info($"restore-blob -c=\"*\" -b=\"*\" -d=\"{blobBackUpFromDate}\" -e=\"{blobBackUpToDate}\" -f=false");
            
        }

        public async Task RestoreBlobAsync(RestoreBlobCommand command)
        {
            var commands = new BlobCommands
            {
                ContainerName = command.ContainerName,
                BlobPath = command.BlobPath,
                FromDate = command.FromDate,
                ToDate = command.ToDate,
                Force = command.Force
            };

            var storageConnection = new StorageConnection();

            var blobOperation = Bootstrap.Container.GetInstance<IBlobOperation>();
            await blobOperation.RestoreAsync(commands, storageConnection);
        }

        public async Task RestoreTableAsync(RestoreTableCommand command)
        {
            var commands = new BlobCommands
            {
                TableName = command.TableName,
                FromDate = command.FromDate,
                ToDate = command.ToDate
            };
            var storageConnection = new StorageConnection();

            var tableOperation = Bootstrap.Container.GetInstance<ITableOperation>();
            await tableOperation.RestoreAsync(commands, storageConnection);
        }
    }
}