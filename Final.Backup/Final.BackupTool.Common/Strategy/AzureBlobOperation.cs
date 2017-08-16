using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Blob;
using Final.BackupTool.Common.Operational;
using Final.BackupTool.Common.Pipelines;

namespace Final.BackupTool.Common.Strategy
{
    public class AzureBlobOperation : IBlobOperation
    {
        public CloudBlobContainer[] ProductionContainerList { get; set; }
        public CloudBlobContainer[] BackupContainerList { get; set; }
        public string DestAccountName { get; set; }
        
        public async Task BackupAsync(StorageConnection storageConnection)
        {
            var pipeline = new BackupBlobStoragePipeline();
            await pipeline.BackupAsync(storageConnection);
        }

        /// <summary>
        /// Restore blob storage
        /// </summary>
        /// <param name="commands"></param>
        /// <param name="storageConnection"></param>
        public async Task RestoreAsync(BlobCommands commands, StorageConnection storageConnection)
        {
            var pipeline = new RestoreBlobStoragePipeline();
            await pipeline.RestoreAsync(commands, storageConnection);
        }
    }
}