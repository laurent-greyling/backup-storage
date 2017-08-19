using System;
using System.Linq;
using System.Threading.Tasks.Dataflow;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.RetryPolicies;
using Microsoft.WindowsAzure.Storage.Table;
using Final.BackupTool.Common.Operational;

namespace Final.BackupTool.Common.Blocks
{
    public class BackupAccountToTableBlock
    {
        public static TransformManyBlock<CloudStorageAccount, CloudTable> Create(StorageConnection sourceStorageAccount)
        {
            return new TransformManyBlock<CloudStorageAccount, CloudTable>(
                account =>
                {
                    var tableClient = sourceStorageAccount.ProductionStorageAccount.CreateCloudTableClient();
                    tableClient.DefaultRequestOptions.RetryPolicy = new ExponentialRetry(TimeSpan.FromSeconds(5), 5);

                    return tableClient.ListTables().Where(c =>
                    {
                        var acceptedContainer = c.Name.ToLowerInvariant();
                        return !acceptedContainer.StartsWith("wad") && // Exclude WAD logs
                               !acceptedContainer.StartsWith("wawsapplogtable") && // Exclude wawsapplogtable tables
                               !acceptedContainer.StartsWith("activities") && // Exclude runtime data
                               !acceptedContainer.StartsWith("stagedfiles");
                    });
                });
        }
    }
}
