using System;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.WindowsAzure.Storage;
using Final.BackupTool.Common.Blocks;
using Final.BackupTool.Common.Operational;

namespace Final.BackupTool.Common.Pipelines
{
    public class BackupTableStoragePipeline
    {
        public async Task BackupAsync()
        {
            var backupOperationStore = new StartBackupTableOperationStore();
            var backupOperation = await backupOperationStore.StartAsync();
            var summary = await ExecuteAsync(backupOperation.Date);
            await backupOperationStore.FinishAsync(backupOperation, summary);
        }

        private async Task<Summary> ExecuteAsync(DateTimeOffset date)
        {
            var pipeline = CreatePipelineAsync(date);

            var storageConnection = new StorageConnection();
            var summary = await pipeline(storageConnection.ProductionStorageAccount);

            return summary;
        }

        private Func<CloudStorageAccount, Task<Summary>> CreatePipelineAsync(DateTimeOffset date)
        {
            var accountToTables = BackupAccountToTableBlock.Create();
            var copyTables = BackupTableBlock.Create(date);

            var summary = new Summary();
            var summarize = SummaryBlock.Create(summary);

            accountToTables.LinkTo(copyTables, new DataflowLinkOptions { PropagateCompletion = true });
            copyTables.LinkTo(summarize, new DataflowLinkOptions { PropagateCompletion = true });

            var flow = DataflowBlock.Encapsulate(accountToTables, copyTables);

            return async account =>
            {
                await flow.SendAsync(account);
                flow.Complete();
                await flow.Completion;

                return summary;
            };
        }
    }
}
