using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.WindowsAzure.Storage;
using Final.BackupTool.Common.Blocks;
using Final.BackupTool.Common.Entities;
using Final.BackupTool.Common.Operational;

namespace Final.BackupTool.Common.Pipelines
{
    public class RestoreTableStoragePipeLine
    {
        public async Task RestoreAsync(BlobCommands commands, StorageConnection storageConnection)
        {
            var restoreOperationStore = new StartRestoreTableOperationStore();
            var restoreOperation = await restoreOperationStore.StartAsync(storageConnection);
            var summary = await ExecuteAsync(commands, storageConnection, restoreOperation.Date);
            await restoreOperationStore.FinishAsync(restoreOperation, summary, storageConnection);
        }

        private async Task<Summary> ExecuteAsync(BlobCommands commands, StorageConnection storageConnection, DateTimeOffset date)
        {
            var pipeline = CreatePipelineAsync(commands, storageConnection, date);

            var summary = await pipeline(storageConnection.BackupStorageAccount);

            return summary;
        }

        private Func<CloudStorageAccount, Task<Summary>> CreatePipelineAsync(BlobCommands commands, StorageConnection storageConnection, DateTimeOffset date)
        {
            try
            {
                var accountToTables = RestoreAccountToTableBlock.Create(storageConnection);
                var restoreTables = RestoreTableBlock.Create(commands, storageConnection, date);

                var summary = new Summary();
                var summarize = SummaryBlock.CreateTableSummary(summary);

                accountToTables.LinkTo(restoreTables, new DataflowLinkOptions { PropagateCompletion = true });
                restoreTables.LinkTo(summarize, new DataflowLinkOptions { PropagateCompletion = true });

                var flow = DataflowBlock.Encapsulate(accountToTables, restoreTables);

                return async account =>
                {
                    await flow.SendAsync(account);
                    flow.Complete();
                    await flow.Completion;

                    return summary;
                };
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw new Exception("Error restoring tables", e);
            }
        }
    }
}
