
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Blob;
using Final.BackupTool.Common.Operational;

namespace Final.BackupTool.Common.Strategy
{
    public interface IBlobOperation
    {
        CloudBlobContainer[] ProductionContainerList { get; set; }
        CloudBlobContainer[] BackupContainerList { get; set; }

        Task BackupAsync(StorageConnection storageConnection);
        Task RestoreAsync(BlobCommands commands, StorageConnection storageConnection);
    }
}