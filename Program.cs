using System;
using System.Data.Services.Common;
using System.Diagnostics;
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

            var swTable = new Stopwatch();
            var swblob = new Stopwatch();

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
            swTable.Start();
            //BackupTableStorage.CopyTableStorage(storageAccount, destStorageAccount);
            BackupTableStorage.CopyAndBackUpTableStorage(storageAccount, destStorageAccount).Wait();
            Console.WriteLine($"Finished copying table storage to new destination storage - {swTable.Elapsed}");
            swTable.Stop();

            Console.WriteLine($"{Environment.NewLine}BLOB STORAGE");
            //Copy and backup blob
            Console.WriteLine("Start copying blob to new destination storage");
            swblob.Start();
            BackupBlobStorage.CopyBlobStorage(storageAccount, destStorageAccount);
            Console.WriteLine($"Finished copying blob to new destination storage - {swblob.Elapsed}");
            swblob.Stop();

            Console.ReadKey();
        }
    }
}
