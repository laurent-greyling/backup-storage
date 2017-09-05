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
        public static IPropagatorBlock<CloudStorageAccount, string> Create(BlobCommands commands)
        {
            var azureOperations = new AzureOperations();
            var containersToRestore = commands.ContainerName.Replace(" ", "").Split(',').ToList();

            return new TransformManyBlock<CloudStorageAccount, string>(
                account =>
                {
                    var blobClient = azureOperations.CreateBackupBlobClient();
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
            return !containerName.StartsWith(OperationalDictionary.Wad) &&
                   !containerName.StartsWith(OperationalDictionary.Azure) &&
                   !containerName.StartsWith(OperationalDictionary.CacheClusterConfigs) &&
                   !containerName.StartsWith(OperationalDictionary.ArmTemplates) &&
                   !containerName.StartsWith(OperationalDictionary.DeploymentLog) &&
                   !containerName.StartsWith(OperationalDictionary.DataDownloads) &&
                   !containerName.StartsWith(OperationalDictionary.Downloads) &&
                   !containerName.StartsWith(OperationalDictionary.StagedDashFiles) &&
                   !containerName.StartsWith(OperationalDictionary.StagedFiles) &&
                   !containerName.Contains(OperationalDictionary.StageArtifacts) &&
                   !containerName.Contains(OperationalDictionary.MyDeployments) &&
                   //on RC storage and for local testing ignore this blob
                   !containerName.Contains(OperationalDictionary.Temporary) &&
                   !containerName.Equals(OperationalDictionary.Logs) &&
                   !containerName.StartsWith(OperationalDictionary.TableBackUpContainerName);
        }
    }
}
