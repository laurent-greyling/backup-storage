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
        public async Task BackupAsync()
        {
            var backupOperationStore = new StartBackUpBlobOperationStore();

            var backupOperation = await backupOperationStore.StartAsync();

            var summary = await ExecuteAsync(backupOperation);

            await backupOperationStore.FinishAsync(backupOperation, summary);
        }

        private async Task<Summary> ExecuteAsync(BlobOperation blobOperation)
        {
            var pipeline = CreatePipelineAsync(blobOperation);

            var azureOperations = new AzureOperations();
            var summary = await pipeline(azureOperations.GetProductionStorageAccount());

            return summary;
        }

        private Func<CloudStorageAccount, Task<Summary>> CreatePipelineAsync(BlobOperation blobOperation)
        {
            var accountToContainers = BackUpAccountToContainersBlock.Create();
            var containers = BackupContainerBlock.Create(blobOperation);
            var logOperationDetailsBlock = BackupLogOperationDetailsBlock.Create(blobOperation);

            var summary = new Summary();
            var summarize = SummaryBlock.Create(summary);

            accountToContainers.LinkTo(containers, new DataflowLinkOptions { PropagateCompletion = true });
            containers.LinkTo(logOperationDetailsBlock, new DataflowLinkOptions { PropagateCompletion = true });
            logOperationDetailsBlock.LinkTo(summarize, new DataflowLinkOptions { PropagateCompletion = true });

            var flow = DataflowBlock.Encapsulate(accountToContainers, logOperationDetailsBlock);

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