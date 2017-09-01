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
        public static IPropagatorBlock<CloudStorageAccount, string> Create()
        {
            var storageConnection = new StorageConnection();
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
                        return ExcludedContainers(n);
                    })
                    .Select(c => c.Name).ToList();

                    return containers;
                }
            );
        }

        private static bool ExcludedContainers(string n)
        {
            return !n.StartsWith(OperationalDictionary.Wad) &&
                   !n.StartsWith(OperationalDictionary.Azure) &&
                   !n.StartsWith(OperationalDictionary.Cacheclusterconfigs) &&
                   !n.StartsWith(OperationalDictionary.ArmTemplates) &&
                   !n.StartsWith(OperationalDictionary.Deploymentlog) &&
                   !n.StartsWith(OperationalDictionary.DataDownloads) &&
                   !n.StartsWith(OperationalDictionary.Downloads) &&
                   !n.StartsWith(OperationalDictionary.StagedDashFiles) &&
                   !n.StartsWith(OperationalDictionary.Stagedfiles) &&
                   !n.Contains(OperationalDictionary.Stageartifacts) &&
                   !n.StartsWith(OperationalDictionary.TableBackUpContainerName);
        }
    }
}
