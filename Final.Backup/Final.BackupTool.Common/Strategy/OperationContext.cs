using System;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.RetryPolicies;
using Final.BackupTool.Common.ConsoleCommand;
using Final.BackupTool.Common.Operational;
using Final.BackupTool.Common.Pipelines;
using Microsoft.Azure;
using NLog;
using NLog.Targets;

namespace Final.BackupTool.Common.Strategy
{
    public class OperationContext : IOperationContext
    {
        public int DaysRetentionAfterDelete { get; }
        private static readonly StorageConnection StorageConnection = new StorageConnection();
        public BackupTableStoragePipeline BackupTablePipeline = new BackupTableStoragePipeline();
        public RestoreTableStoragePipeLine RestoreTablePipeline = new RestoreTableStoragePipeLine();
        public BackupBlobStoragePipeline BackUpBlobPipeline = new BackupBlobStoragePipeline();
        public RestoreBlobStoragePipeline RestoreBlobPipeline = new RestoreBlobStoragePipeline();
        public ILogger Logger;
        
        public OperationContext(ILogger logger)
        {
            Logger = logger;
            // Log version
            Logger.Info($"************VERSION {Assembly.GetExecutingAssembly().GetName().Version}*****************");
            logger.Info("Starting backup tool");

            // Read configuration
            logger.Info("Operation Context - reading configuration...");

            BackupStorageLooksLikeProductionStorage();
            CreateOperationalLogTables();

            // Retrieve other config options
            var daysRetentionAfterDelete = CloudConfigurationManager.GetSetting("DaysRetentionAfterDelete");
            DaysRetentionAfterDelete = int.TryParse(daysRetentionAfterDelete, out int daysRetention) ? daysRetention : 60;

            SetRequestOptions();
        }

        public async Task BackupAsync(BackupCommand command)
        {
            // Both these commands are already heavily parallel, so we just run them in order here
            // so they don't fight over bandwidth amongst themselves
            var tableBackUpFromDate = DateTimeOffset.UtcNow.ToString(OperationalDictionary.DateFormat,
                    CultureInfo.InvariantCulture);
            if (string.IsNullOrEmpty(command.Skip) || command.Skip.ToLowerInvariant() != "tables")
            {
                await BackupTablePipeline.BackupAsync();
            }
            var tableBackUpToDate = DateTimeOffset.UtcNow.AddSeconds(3).ToString(OperationalDictionary.DateFormat, CultureInfo.InvariantCulture);
            
            var blobBackUpFromDate = DateTimeOffset.UtcNow.ToString(OperationalDictionary.DateFormat, CultureInfo.InvariantCulture);
            if (string.IsNullOrEmpty(command.Skip) || command.Skip.ToLowerInvariant() != "blobs")
            {
                await BackUpBlobPipeline.BackupAsync();
            }
            var blobBackUpToDate = DateTimeOffset.UtcNow.AddSeconds(3).ToString(OperationalDictionary.DateFormat, CultureInfo.InvariantCulture);

            Logger.Info("===>USE FOR RESTORING<===");
            Logger.Info($"restore-table -t=\"*\" -d=\"{tableBackUpFromDate}\" -e=\"{tableBackUpToDate}\"");
            Logger.Info($"restore-blob -c=\"*\" -b=\"*\" -d=\"{blobBackUpFromDate}\" -e=\"{blobBackUpToDate}\" -f=false");
            Logger.Info($"restore-all -d=\"{tableBackUpFromDate}\" -e=\"{blobBackUpToDate}\"");
        }

        public async Task RestoreAll(RestoreCommand command)
        {
            var commands = new BlobCommands
            {
                ContainerName = command.ContainerName,
                BlobPath = command.BlobPath,
                TableName = command.TableName,
                FromDate = command.FromDate,
                ToDate = command.ToDate,
                Force = command.Force
            };
            
            await RestoreTablePipeline.RestoreAsync(commands);
            
            await RestoreBlobPipeline.RestoreAsync(commands);
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
            
            await RestoreBlobPipeline.RestoreAsync(commands);
        }

        public async Task RestoreTableAsync(RestoreTableCommand command)
        {
            var commands = new BlobCommands
            {
                TableName = command.TableName,
                FromDate = command.FromDate,
                ToDate = command.ToDate
            };
            
            await RestoreTablePipeline.RestoreAsync(commands);
        }

        public async Task StoreLogInStorage()
        {
            var operationalAccount = StorageConnection.OperationalAccount;

            var operationalClient = operationalAccount.CreateCloudBlobClient();
            var container = operationalClient.GetContainerReference(OperationalDictionary.LogContainer);

            container.CreateIfNotExists();

            var fileTarget = (FileTarget)LogManager.Configuration.FindTargetByName("f");
            var logEventInfo = new LogEventInfo { TimeStamp = DateTime.Now };
            var filePath = Path.GetFullPath(fileTarget.FileName.Render(logEventInfo));

            var blob = container.GetAppendBlobReference(Path.GetFileName(filePath));

            await blob.CreateOrReplaceAsync();

            using (var file = File.OpenRead(filePath))
            {
                await blob.AppendFromStreamAsync(file);
            }

            if (File.Exists(filePath)) File.Delete(filePath);
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