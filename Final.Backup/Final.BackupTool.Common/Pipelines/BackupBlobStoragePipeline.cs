using System;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.WindowsAzure.Storage;
using Final.BackupTool.Common.Blocks;
using Final.BackupTool.Common.Operational;

namespace Final.BackupTool.Common.Pipelines
{

    public class BackupBlobStoragePipeline
    {
        public async Task BackupAsync(StorageConnection storageConnection)
        {
            var backupOperationStore = new StartBackUpBlobOperationStore();

            var backupOperation = await backupOperationStore.StartAsync(storageConnection);

            var summary = await ExecuteAsync(backupOperation, storageConnection);

            await backupOperationStore.FinishAsync(backupOperation, summary, storageConnection);
        }

        private async Task<Summary> ExecuteAsync(BlobOperation blobOperation, StorageConnection storageConnection)
        {
            var pipeline = CreatePipelineAsync(blobOperation, storageConnection);

            var summary = await pipeline(storageConnection.ProductionStorageAccount);

            return summary;
        }

        private Func<CloudStorageAccount,Task<Summary>> CreatePipelineAsync(BlobOperation blobOperation, StorageConnection storageConnection)
        {
            var accountToContainers = BackUpAccountToContainersBlock.Create(storageConnection);
            var containers = BackupContainerBlock.Create(blobOperation, storageConnection);
            var logOperationDetailsBlock = BackupLogOperationDetailsBlock.Create(blobOperation, storageConnection);

            var summary = new Summary();
            var summarize = SummaryBlock.Create(summary);

            accountToContainers.LinkTo(containers, new DataflowLinkOptions { PropagateCompletion = true });
            containers.LinkTo(logOperationDetailsBlock, new DataflowLinkOptions { PropagateCompletion = true });
            logOperationDetailsBlock.LinkTo(summarize, new DataflowLinkOptions { PropagateCompletion = true });

            var flow =  DataflowBlock.Encapsulate(accountToContainers, logOperationDetailsBlock);

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