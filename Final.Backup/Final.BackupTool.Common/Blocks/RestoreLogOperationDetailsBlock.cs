using System.Threading.Tasks.Dataflow;
using Final.BackupTool.Common.Operational;

namespace Final.BackupTool.Common.Blocks
{
    public static class RestoreLogOperationDetailsBlock
    {
        public static IPropagatorBlock<CopyStorageOperation, CopyStorageOperation> Create(BlobOperation blobOperation,
            StorageConnection storageConnection)
        {
            var batchBlock = new BatchBlock<CopyStorageOperation>(100);

            var operationStore = new StartRestoreBlobOperationStore();

            var logBlock = new TransformManyBlock<CopyStorageOperation[], CopyStorageOperation>(
                async operations =>
                {
                    await operationStore.WriteCopyOutcomeAsync(blobOperation.Date, operations, storageConnection);

                    return operations;
                });

            batchBlock.LinkTo(logBlock, new DataflowLinkOptions {PropagateCompletion = true});
            return DataflowBlock.Encapsulate(batchBlock, logBlock);
        }
    }
}
