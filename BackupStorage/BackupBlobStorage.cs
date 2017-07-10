using System;
using System.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using backup_storage.Entity;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.RetryPolicies;

namespace backup_storage.BackupStorage
{
    public class BackupBlobStorage
    {
        /// <summary>
        /// Copy and Backup blob storage in paralell manner
        /// </summary>
        /// <param name="storageAccount"></param>
        /// <param name="destStorageAccount"></param>
        public static void CopyBlobStorage(CloudStorageAccount storageAccount, CloudStorageAccount destStorageAccount)
        {
            var blobClient = storageAccount.CreateCloudBlobClient();
            var containers = blobClient.ListContainers().Where(c =>
            {
                var acceptedContainer = c.Name.ToLowerInvariant();
                return !acceptedContainer.Contains("nameofcontainernottocopy");
            });

            Parallel.ForEach(containers, container =>
            {
                var blobClientDest = destStorageAccount.CreateCloudBlobClient();
                var ctn = blobClientDest.GetContainerReference(container.Name);
                
                ctn.CreateIfNotExists();

                CopyBlobsAndBackup(container, ctn);
            });
        }

        private static void CopyBlobsAndBackup(
            CloudBlobContainer srcContainer,
            CloudBlobContainer destContainer)
        {

            var policyId = new SharedAccessBlobPolicy
            {
                SharedAccessExpiryTime = DateTimeOffset.UtcNow.AddHours(24),
                Permissions = SharedAccessBlobPermissions.Read
            };

            // get the SAS token to use for all blobs
            var sas = srcContainer.GetSharedAccessSignature(policyId);
            
            var srcBlobList = srcContainer.ListBlobs();

            Parallel.ForEach(srcBlobList, async src =>
            {
                var srcBlob = (CloudBlob) src;
                
                CloudBlob destBlob = destContainer.GetBlockBlobReference(srcBlob.Name);

                var srcBlockBlobSasUri = $"{srcBlob.Uri}{sas}";
                // copy using src blob as SAS
                await destBlob.StartCopyAsync(new Uri(srcBlockBlobSasUri));
            });
        }

        /// <summary>
        /// Copy and backup blob in dataflow manner
        /// </summary>
        /// <returns></returns>
        public static async Task BackupBlobToStorage(CloudStorageAccount storageAccount,
            CloudStorageAccount destStorageAccount)
        {
            var fromAccountToContainers = new TransformManyBlock<CloudStorageAccount, CloudBlobContainer>(
               account =>
               {
                   var blobClient = storageAccount.CreateCloudBlobClient();
                   blobClient.DefaultRequestOptions.RetryPolicy = new ExponentialRetry(TimeSpan.FromSeconds(5), 5);

                   return blobClient.ListContainers().Where(c =>
                   {
                       var acceptedContainer = c.Name.ToLowerInvariant();
                       return !acceptedContainer.Contains("nameofcontainernottocopy");
                   });
               },
               new ExecutionDataflowBlockOptions
               {
                   MaxDegreeOfParallelism = 20,
                   BoundedCapacity = 80
               });

            var fromContainerToBlob = new ActionBlock<CloudBlobContainer>(async cntr =>
                {
                    var policyId = new SharedAccessBlobPolicy
                    {
                        SharedAccessExpiryTime = DateTimeOffset.UtcNow.AddHours(24),
                        Permissions = SharedAccessBlobPermissions.Read
                    };

                    // get the SAS token to use for all blobs
                    var sas = cntr.GetSharedAccessSignature(policyId);
                    var blobClientDest = destStorageAccount.CreateCloudBlobClient();
                    var ctn = blobClientDest.GetContainerReference(cntr.Name);

                    ctn.CreateIfNotExists();

                    var batchOp = new BatchBlock<BlobItem>(40);
                    var lBlobItems = cntr.ListBlobs(useFlatBlobListing: true).Cast<CloudBlob>()
                        .Select(blobs => batchOp.SendAsync(new BlobItem
                        {
                            BlobName = blobs.Name,
                            Blob = $"{blobs.Uri}{sas}"
                        })).ToArray();

                    var copyToDestination = new ActionBlock<BlobItem[]>(bli =>
                        {
                            Parallel.ForEach(bli, async blobItem =>
                            {
                                CloudBlob destBlob = ctn.GetBlockBlobReference(blobItem.BlobName);
                                await destBlob.StartCopyAsync(new Uri(blobItem.Blob));
                            });
                        },
                        new ExecutionDataflowBlockOptions
                        {
                            MaxDegreeOfParallelism = 20,
                            BoundedCapacity = 80
                        });

                    batchOp.LinkTo(copyToDestination);

                    batchOp.Complete();
                    await batchOp.Completion;
                    copyToDestination.Complete();
                    await copyToDestination.Completion;
                },
                new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = 20,
                    BoundedCapacity = 80
                });

            fromAccountToContainers.LinkTo(fromContainerToBlob);

            await fromAccountToContainers.SendAsync(storageAccount);

            fromAccountToContainers.Complete();
            await fromAccountToContainers.Completion;
            fromContainerToBlob.Complete();
            await fromContainerToBlob.Completion;
        }
    }
}