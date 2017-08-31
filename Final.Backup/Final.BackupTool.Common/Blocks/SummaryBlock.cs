using System.Threading.Tasks.Dataflow;
using Final.BackupTool.Common.Operational;

namespace Final.BackupTool.Common.Blocks
{
    public class SummaryBlock
    {
        public static ITargetBlock<CopyStorageOperation> Create(Summary summary)
        {
            var result = new ActionBlock<CopyStorageOperation>(
                operation =>
                {
                    if (operation.CopyStatus == StorageCopyStatus.Completed)
                        ++summary.Copied;

                    if (operation.CopyStatus == StorageCopyStatus.Skipped)
                        ++summary.Skipped;

                    if (operation.CopyStatus == StorageCopyStatus.Faulted)
                        ++summary.Faulted;
                });
            return result;
        }
    }
}
