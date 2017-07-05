using System;
using System.IO;
using System.Text;
using Microsoft.WindowsAzure.Storage;

namespace backup_storage.Shared
{
    public class CreateBlobStorage
    {
        public static void CreateAndPopulateBlob(CloudStorageAccount storageAccount)
        {
            var blobClient = storageAccount.CreateCloudBlobClient();

            var container = blobClient.GetContainerReference("mycontainer");

            // Create the container if it doesn't already exist.
            container.CreateIfNotExists();

            var containers = blobClient.ListContainers();

            var i = 0;

            foreach (var contain in containers)
            {
                if (!contain.Exists()) continue;
                i++;

                var id = Guid.NewGuid();

                var blockBlob = container.GetBlockBlobReference($"myBlob{id}");

                using (var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes($"This is an id {id}")))
                {
                    blockBlob.UploadFromStream(memoryStream);
                }

                container = blobClient.GetContainerReference($"mycontainer{i}");
                container.CreateIfNotExists();
            }
        }
    }
}