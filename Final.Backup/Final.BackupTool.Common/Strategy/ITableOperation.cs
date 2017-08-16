using System;
using System.Threading.Tasks;
using Final.BackupTool.Common.Operational;

namespace Final.BackupTool.Common.Strategy
{
    public interface ITableOperation
    {
        Task BackupAsync(StorageConnection storageConnection);
        Task RestoreAsync(BlobCommands commands, StorageConnection storageConnection);
    }
}