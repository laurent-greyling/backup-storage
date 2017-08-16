using System;
using System.Linq;
using System.Threading.Tasks.Dataflow;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.RetryPolicies;
using Final.BackupTool.Common.Operational;

namespace Final.BackupTool.Common.Blocks
{
    public static class RestoreAccountToContainersBlock
    {
        public static IPropagatorBlock<CloudStorageAccount, string> Create(StorageConnection storageConnection, BlobCommands commands)
        {
            var storageAccount = storageConnection.BackupStorageAccount;
            var containersToRestore = commands.ContainerName.Replace(" ", "").Split(',').ToList();

            return new TransformManyBlock<CloudStorageAccount, string>(
                account =>
                {
                    var blobClient = storageAccount.CreateCloudBlobClient();
                    blobClient.DefaultRequestOptions.RetryPolicy = new ExponentialRetry(TimeSpan.FromSeconds(5), 5);
                    var containers = containersToRestore.Contains("*")
                        ? blobClient.ListContainers().Where(c =>
                        {
                            var n = c.Name.ToLowerInvariant();
                            return ExcludedContainers(n);
                        }).Select(c => c.Name).ToList()
                        : blobClient.ListContainers().Where(c =>
                        {
                            var n = c.Name.ToLowerInvariant();
                            return ExcludedContainers(n) && containersToRestore.Any(b => b == c.Name);
                        }).Select(c => c.Name).ToList();

                    return containers;
                }
            );
        }

        private static bool ExcludedContainers(string containerName)
        {
            return !containerName.StartsWith("wad") &&
                   !containerName.StartsWith("azure") &&
                   !containerName.StartsWith("cacheclusterconfigs") &&
                   !containerName.StartsWith("arm-templates") &&
                   !containerName.StartsWith("deploymentlog") &&
                   !containerName.StartsWith("data-downloads") &&
                   !containerName.StartsWith("downloads") &&
                   !containerName.StartsWith("staged-files") &&
                   !containerName.StartsWith("stagedfiles") &&
                   !containerName.Contains("stageartifacts") &&
                   !containerName.Contains("mydeployments") && //on RC storage and for local testing ignore this blob
                   !containerName.Contains("temporary") &&
                   !containerName.StartsWith(OperationalDictionary.TableBackUpContainerName);
        }
    }
}
