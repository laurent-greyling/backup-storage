using System;
using System.Globalization;
using System.Threading.Tasks;
using Final.BackupTool.Common.ConsoleCommand;
using Final.BackupTool.Common.Operational;
using Final.BackupTool.Common.Pipelines;
using Microsoft.Azure;
using NLog;

namespace Final.BackupTool.Common.Strategy
{
    public class OperationContext : IOperationContext
    {
        public int DaysRetentionAfterDelete { get; set; }
        public ILogger Logger;
        private readonly BackupTableStoragePipeline _backupTablePipeline = new BackupTableStoragePipeline();
        private readonly RestoreTableStoragePipeLine _restoreTablePipeline = new RestoreTableStoragePipeLine();
        private readonly BackupBlobStoragePipeline _backUpBlobPipeline = new BackupBlobStoragePipeline();
        private readonly RestoreBlobStoragePipeline _restoreBlobPipeline = new RestoreBlobStoragePipeline();

        public OperationContext(ILogger logger)
        {
            Logger = logger;
            var initializeOperation = new InitializeOperation();
            initializeOperation.Execute(Logger);

            // Retrieve other config options
            var daysRetentionAfterDelete = CloudConfigurationManager.GetSetting("DaysRetentionAfterDelete");
            DaysRetentionAfterDelete = int.TryParse(daysRetentionAfterDelete, out int daysRetention) ? daysRetention : 60;
        }

        public async Task BackupAsync(BackupCommand command)
        {
            // Both these commands are already heavily parallel, so we just run them in order here
            // so they don't fight over bandwidth amongst themselves
            var tableBackUpFromDate = DateTimeOffset.UtcNow.ToString(OperationalDictionary.DateFormat, CultureInfo.InvariantCulture);
            if (string.IsNullOrEmpty(command.Skip) || command.Skip.ToLowerInvariant() != "tables")
            {
                await _backupTablePipeline.BackupAsync();
            }
            var tableBackUpToDate = DateTimeOffset.UtcNow.AddSeconds(3).ToString(OperationalDictionary.DateFormat, CultureInfo.InvariantCulture);

            var blobBackUpFromDate = DateTimeOffset.UtcNow.ToString(OperationalDictionary.DateFormat, CultureInfo.InvariantCulture);
            if (string.IsNullOrEmpty(command.Skip) || command.Skip.ToLowerInvariant() != "blobs")
            {
                await _backUpBlobPipeline.BackupAsync();
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

            await _restoreTablePipeline.RestoreAsync(commands);
            await _restoreBlobPipeline.RestoreAsync(commands);
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

            await _restoreBlobPipeline.RestoreAsync(commands);
        }

        public async Task RestoreTableAsync(RestoreTableCommand command)
        {
            var commands = new BlobCommands
            {
                TableName = command.TableName,
                FromDate = command.FromDate,
                ToDate = command.ToDate
            };

            await _restoreTablePipeline.RestoreAsync(commands);
        }

        public async Task StoreLogInStorage()
        {
            var storeLogs = new StoreLogFile();
            await storeLogs.Save();
        }
    }
}