using System;
using System.Data.Services.Common;
using System.Threading.Tasks;
using backup_storage.Entity;
using backup_storage.Shared;
using Microsoft.Azure;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;

namespace backup_storage
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var storageAccount = CloudStorageAccount.Parse(CloudConfigurationManager.GetSetting("StorageConnectionString"));
            var destStorageAccount = CloudStorageAccount.Parse(CloudConfigurationManager.GetSetting("DestStorageConnectionString"));
            
            //Create and populate stuff
            Console.WriteLine("Creating and populating some more dummy tables....");
            CreateTableStorage.CreateAndPopulateTable(storageAccount);
            Console.WriteLine("Finished Creating and populating some more dummy tables....");

            Console.WriteLine("Creating and populating some more dummy blobs....");
            CreateBlobStorage.CreateAndPopulateBlob(storageAccount);
            Console.WriteLine("Finished Creating and populating some more dummy blobs....");

            //Copy and backup table storage
            Console.WriteLine($"{Environment.NewLine}TABLE STORAGE");
            Console.WriteLine("Start copying table storage to new destination storage");
            BackupTableStorage.CopyTableStorage(storageAccount, destStorageAccount);
            Console.WriteLine("Finished copying table storage to new destination storage");

            Console.WriteLine($"{Environment.NewLine}BLOB STORAGE");
            //Copy and backup blob
            Console.WriteLine("Start copying blob to new destination storage");
            BackupBlobStorage.ContainerCopyAndBackUp(storageAccount, destStorageAccount);
            Console.WriteLine("Finished copying blob to new destination storage");

            Console.ReadKey();
        }
    }
}
