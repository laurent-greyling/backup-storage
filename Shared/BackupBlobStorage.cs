using System;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace backup_storage.Shared
{
    public class BackupBlobStorage
    {
        public static void CopyBlobStorage(CloudStorageAccount storageAccount, CloudStorageAccount destStorageAccount)
        {
            var blobClient = storageAccount.CreateCloudBlobClient();
            var containers = blobClient.ListContainers();

            Parallel.ForEach(containers, container =>
            {
                var blobClientDest = destStorageAccount.CreateCloudBlobClient();
                var ctn = blobClientDest.GetContainerReference(container.Name);
                
                ctn.CreateIfNotExists();

                CopyBlobsAndBackup(container, ctn);
            });
        }

        public static void CopyBlobsAndBackup(
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

                // Create appropriate destination blob type to match the source blob
                CloudBlob destBlob;
                if (srcBlob.GetType() == typeof(CloudBlockBlob))
                {
                    srcBlob = (CloudBlockBlob) src;
                    destBlob = destContainer.GetBlockBlobReference(srcBlob.Name);
                }
                else
                {
                    destBlob = destContainer.GetPageBlobReference(srcBlob.Name);
                }

                var srcBlockBlobSasUri = $"{srcBlob.Uri}{sas}";
                // copy using src blob as SAS
                await destBlob.StartCopyAsync(new Uri(srcBlockBlobSasUri));
            });
        }
    }
}