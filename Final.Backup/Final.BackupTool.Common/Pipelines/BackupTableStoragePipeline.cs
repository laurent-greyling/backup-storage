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
        public async Task BackupAsync(StorageConnection storageConnection)
        {
            var backupOperationStore = new StartBackupTableOperationStore();
            var backupOperation = await backupOperationStore.StartAsync(storageConnection);
            var summary = await ExecuteAsync(storageConnection, backupOperation.Date);
            await backupOperationStore.FinishAsync(backupOperation, summary, storageConnection);
        }

        private async Task<Summary> ExecuteAsync(StorageConnection storageConnection, DateTimeOffset date)
        {
            var pipeline = CreatePipelineAsync(storageConnection, date);

            var summary = await pipeline(storageConnection.ProductionStorageAccount);

            return summary;
        }

        private Func<CloudStorageAccount, Task<Summary>> CreatePipelineAsync(StorageConnection storageConnection, DateTimeOffset date)
        {
            var accountToTables = BackupAccountToTableBlock.Create(storageConnection);
            var copyTables = BackupTableBlock.Create(storageConnection, date);

            var summary = new Summary();
            var summarize = SummaryBlock.CreateTableSummary(summary);

            accountToTables.LinkTo(copyTables, new DataflowLinkOptions { PropagateCompletion = true });
            copyTables.LinkTo(summarize, new DataflowLinkOptions {PropagateCompletion = true});

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
