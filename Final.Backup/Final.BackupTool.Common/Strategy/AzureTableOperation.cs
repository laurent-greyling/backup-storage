using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Final.BackupTool.Common.Operational;
using Final.BackupTool.Common.Pipelines;
using NLog;

namespace Final.BackupTool.Common.Strategy
{
    public class AzureTableOperation : ITableOperation
    {
        private readonly ILogger _logger;

        public AzureTableOperation(ILogger logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Copy and backup table storage into blob
        /// </summary>
        /// <returns></returns>
        public async Task BackupAsync(StorageConnection storageConnection)
        {
            try
            {
                var sw = new Stopwatch();

                _logger.Info($"Start Backup of Tables on {DateTime.UtcNow}");
                sw.Start();

                var pipeline = new BackupTableStoragePipeline();
                await pipeline.BackupAsync(storageConnection);

                sw.Stop();
                _logger.Info($"Finished Backup of Tables in {sw.Elapsed} minutes on the {DateTime.UtcNow}");
            }
            catch (Exception e)
            {
                _logger.Error(e, e.Message);
            }
        }

        /// <summary>
        /// Restore table storage from manifest files
        /// </summary>
        public async Task RestoreAsync(BlobCommands commands, StorageConnection storageConnection)
        {
            try
            {
                var sw = new Stopwatch();
                _logger.Info($"Start restoring Tables on {DateTime.UtcNow}");
                sw.Start();

                var pipeline = new RestoreTableStoragePipeLine();
                await pipeline.RestoreAsync(commands, storageConnection);

                sw.Stop();
                _logger.Info($"Finished restoring Tables in {sw.Elapsed} minutes on the {DateTime.UtcNow}");
            }
            catch (Exception e)
            {
                _logger.Error(e, e.Message);
            }
            
        }
    }
}