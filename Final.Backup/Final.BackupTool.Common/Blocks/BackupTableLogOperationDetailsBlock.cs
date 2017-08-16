using System.Threading.Tasks.Dataflow;
using Final.BackupTool.Common.Operational;

namespace Final.BackupTool.Common.Blocks
{
    public class BackupTableLogOperationDetailsBlock
    {
        public static IPropagatorBlock<CopyStorageOperation, CopyStorageOperation> Create(BlobOperation blobOperation, StorageConnection storageConnection)
        {
            var batchBlock = new BatchBlock<CopyStorageOperation>(100);

            var operationStore = new StartBackupTableOperationStore();

            var logBlock = new TransformManyBlock<CopyStorageOperation[], CopyStorageOperation>(
                operations =>
                {
                    operationStore.WriteCopyOutcomeAsync(blobOperation.Date, operations, storageConnection).Wait();

                    return operations;
                });

            batchBlock.LinkTo(logBlock, new DataflowLinkOptions { PropagateCompletion = true });
            return DataflowBlock.Encapsulate(batchBlock, logBlock);
        }
    }
}
