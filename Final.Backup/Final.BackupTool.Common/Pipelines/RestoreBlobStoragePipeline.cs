using System;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.WindowsAzure.Storage;
using Final.BackupTool.Common.Blocks;
using Final.BackupTool.Common.Operational;

namespace Final.BackupTool.Common.Pipelines
{

    public class RestoreBlobStoragePipeline
    {
        public async Task RestoreAsync(BlobCommands commands)
        {
            var restoreOperationStore = new StartRestoreBlobOperationStore();

            var restoreOperation = await restoreOperationStore.StartAsync();

            var summary = await ExecuteAsync(restoreOperation, commands);

            await restoreOperationStore.FinishAsync(restoreOperation, summary);
        }

        private async Task<Summary> ExecuteAsync(BlobOperation blobOperation, BlobCommands commands)
        {
            var pipeline = CreatePipelineAsync(blobOperation, commands);

            var storageConnection = new StorageConnection();
            var summary = await pipeline(storageConnection.BackupStorageAccount);

            return summary;
        }

        private Func<CloudStorageAccount,Task<Summary>> CreatePipelineAsync(BlobOperation blobOperation, BlobCommands commands)
        {
            var accountToContainers = RestoreAccountToContainersBlock.Create(commands);
            var containers = RestoreContainerBlock.Create(blobOperation, commands);
            var logOperationDetailsBlock = RestoreLogOperationDetailsBlock.Create(blobOperation);

            var summary = new Summary();
            var summarize = SummaryBlock.Create(summary);

            accountToContainers.LinkTo(containers, new DataflowLinkOptions { PropagateCompletion = true });
            containers.LinkTo(logOperationDetailsBlock, new DataflowLinkOptions { PropagateCompletion = true });
            logOperationDetailsBlock.LinkTo(summarize, new DataflowLinkOptions { PropagateCompletion = true });

            var flow =  DataflowBlock.Encapsulate(accountToContainers, logOperationDetailsBlock);

            return async (account) =>
            {
                await flow.SendAsync(account);
                flow.Complete();
                await flow.Completion;

                return summary;
            };
        }
    }
}