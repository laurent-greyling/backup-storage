using System.Threading.Tasks;
using Final.BackupTool.Common.ConsoleCommand;

namespace Final.BackupTool.Common.Strategy
{
    public interface IOperationContext
    {
        Task BackupAsync(BackupCommand command);
        Task RestoreAll(RestoreCommand command);
        Task RestoreBlobAsync(RestoreBlobCommand command);
        Task RestoreTableAsync(RestoreTableCommand command);
        int DaysRetentionAfterDelete { get; }
        Task StoreLogInStorage();
    }
}