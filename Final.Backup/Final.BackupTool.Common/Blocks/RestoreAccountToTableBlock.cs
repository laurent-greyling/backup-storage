using System;
using System.Threading.Tasks.Dataflow;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.RetryPolicies;
using Final.BackupTool.Common.Operational;

namespace Final.BackupTool.Common.Blocks
{
    public class RestoreAccountToTableBlock
    {
        public static TransformBlock<CloudStorageAccount, CloudBlobContainer> Create()
        {
            var storageConnection = new StorageConnection();
            var fromAccountToContainers = new TransformBlock<CloudStorageAccount, CloudBlobContainer>(
                account =>
                {
                    var blobClient = storageConnection.BackupStorageAccount.CreateCloudBlobClient();
                    blobClient.DefaultRequestOptions.RetryPolicy = new ExponentialRetry(TimeSpan.FromSeconds(5), 5);

                    return blobClient.GetContainerReference(OperationalDictionary.TableBackupContainer);
                });
            return fromAccountToContainers;
        }
    }
}
