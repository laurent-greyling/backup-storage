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
        public async Task RestoreAsync(BlobCommands commands, StorageConnection storageConnection)
        {
            var restoreOperationStore = new StartRestoreBlobOperationStore();

            var restoreOperation = await restoreOperationStore.StartAsync(storageConnection);

            var summary = await ExecuteAsync(restoreOperation, storageConnection, commands);

            await restoreOperationStore.FinishAsync(restoreOperation, summary, storageConnection);
        }

        private async Task<Summary> ExecuteAsync(BlobOperation blobOperation, StorageConnection storageConnection, BlobCommands commands)
        {
            var pipeline = CreatePipelineAsync(blobOperation, storageConnection, commands);

            var summary = await pipeline(storageConnection.BackupStorageAccount);

            return summary;
        }

        private Func<CloudStorageAccount,Task<Summary>> CreatePipelineAsync(BlobOperation blobOperation, StorageConnection storageConnection,
            BlobCommands commands)
        {
            var accountToContainers = RestoreAccountToContainersBlock.Create(storageConnection, commands);
            var containers = RestoreContainerBlock.Create(blobOperation, storageConnection, commands);
            var logOperationDetailsBlock = RestoreLogOperationDetailsBlock.Create(blobOperation, storageConnection);

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