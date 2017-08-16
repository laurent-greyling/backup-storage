using System;
using System.Linq;
using System.Threading.Tasks.Dataflow;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.RetryPolicies;
using Final.BackupTool.Common.Operational;

namespace Final.BackupTool.Common.Blocks
{
    public static class BackUpAccountToContainersBlock
    {
        public static IPropagatorBlock<CloudStorageAccount, string> Create(StorageConnection storageConnection)
        {
            var storageAccount = storageConnection.ProductionStorageAccount;
            return new TransformManyBlock<CloudStorageAccount, string>(
                account =>
                {
                    var blobClient = storageAccount.CreateCloudBlobClient();
                    blobClient.DefaultRequestOptions.RetryPolicy = new ExponentialRetry(TimeSpan.FromSeconds(5), 5);
                    var containers = blobClient.ListContainers()
                    .Where(c =>
                    {
                        var n = c.Name.ToLowerInvariant();
                        return !n.StartsWith("wad") &&
                               !n.StartsWith("azure") &&
                               !n.StartsWith("cacheclusterconfigs") &&
                               !n.StartsWith("arm-templates") &&
                               !n.StartsWith("deploymentlog") &&
                               !n.StartsWith("data-downloads") &&
                               !n.StartsWith("downloads") &&
                               !n.StartsWith("staged-files") &&
                               !n.StartsWith("stagedfiles") &&
                               !n.Contains("stageartifacts") &&
                               !n.StartsWith(OperationalDictionary.TableBackUpContainerName);
                    })
                    .Select(c => c.Name).ToList();

                    return containers;
                }
            );
        }
    }
}
