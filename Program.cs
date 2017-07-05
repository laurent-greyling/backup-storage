using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using backup_storage.Entity;
using backup_storage.Shared;
using Microsoft.Azure;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using Microsoft.WindowsAzure.Storage.Blob;

namespace backup_storage
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var storageAccount = CloudStorageAccount.Parse(CloudConfigurationManager.GetSetting("StorageConnectionString"));

            CreateTableStorage.CreateAndPopulateTable(storageAccount);

            CreateBlobStorage.CreateAndPopulateBlob(storageAccount);
        }
    }
}
