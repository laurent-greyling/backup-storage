using System;
using backup_storage.Shared;
using Microsoft.Azure;
using Microsoft.WindowsAzure.Storage;

namespace backup_storage
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var storageAccount = CloudStorageAccount.Parse(CloudConfigurationManager.GetSetting("StorageConnectionString"));
            var destStorageAccount = CloudStorageAccount.Parse(CloudConfigurationManager.GetSetting("DestStorageConnectionString"));

            Console.WriteLine("Creating and populating some more dummy tables....");
            CreateTableStorage.CreateAndPopulateTable(storageAccount);
            Console.WriteLine("Finished Creating and populating some more dummy tables....");

            Console.WriteLine("Creating and populating some more dummy blobs....");
            CreateBlobStorage.CreateAndPopulateBlob(storageAccount);
            Console.WriteLine("Creating and populating some more dummy blobs....");

            Console.WriteLine("Start copying blob to new destination storage");
            BackupBlobStorage.ContainerCopyAndBackUp(storageAccount, destStorageAccount);
            Console.WriteLine("Finished copying blob to new destination storage");

            Console.ReadKey();
        }
    }
}
