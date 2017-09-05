using System;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.WindowsAzure.Storage;
using Final.BackupTool.Common.Blocks;
using Final.BackupTool.Common.Operational;

namespace Final.BackupTool.Common.Pipelines
{
    public class RestoreTableStoragePipeLine
    {
        public async Task RestoreAsync(BlobCommands commands)
        {
            var restoreOperationStore = new StartRestoreTableOperationStore();
            var restoreOperation = await restoreOperationStore.StartAsync();
            var summary = await ExecuteAsync(commands, restoreOperation.Date);
            await restoreOperationStore.FinishAsync(restoreOperation, summary);
        }

        private async Task<Summary> ExecuteAsync(BlobCommands commands, DateTimeOffset date)
        {
            var pipeline = CreatePipelineAsync(commands, date);

            var azureOperations = new AzureOperations();
            var summary = await pipeline(azureOperations.GetBackupStorageAccount());

            return summary;
        }

        private Func<CloudStorageAccount, Task<Summary>> CreatePipelineAsync(BlobCommands commands, DateTimeOffset date)
        {
            try
            {
                var accountToTables = RestoreAccountToTableBlock.Create();
                var restoreTables = RestoreTableBlock.Create(commands, date);

                var summary = new Summary();
                var summarize = SummaryBlock.Create(summary);

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
                Console.Error.WriteLine(e);
                throw new Exception("Error restoring tables", e);
            }
        }
    }
}
