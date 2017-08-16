using System.Threading.Tasks;
using Final.BackupTool.Common.ConsoleCommand;

namespace Final.BackupTool.Common.Strategy
{
    public interface IOperationContext
    {
        Task BackupAsync();
        Task RestoreBlobAsync(RestoreBlobCommand command);
        Task RestoreTableAsync(RestoreTableCommand command);
        int DaysRetentionAfterDelete { get; }
    }
}