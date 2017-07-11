using System;
using System.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using backup_storage.Entity;
using backup_storage.Shared;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.RetryPolicies;
using Microsoft.WindowsAzure.Storage.Table;

namespace backup_storage.RestoreStorage
{
    public class RestoreTableStorage
    {
        /// <summary>
        /// Restore Tables from backup table storage
        /// </summary>
        /// <param name="tablesToRestore"></param>
        /// <param name="storageAccount"></param>
        /// <param name="destStorageAccount"></param>
        /// <returns></returns>
        public static async Task CopyAndRestoreTableStorageAsync(string tablesToRestore, CloudStorageAccount storageAccount,
            CloudStorageAccount destStorageAccount)
        {
            //Specified tables to be restored
            var tables = tablesToRestore.Split(',').ToList();

            if (tables.Count > 0)
            {
                var fromAccountToTables = new TransformManyBlock<CloudStorageAccount, CloudTable>(
                 account =>
                 {
                     var tableClient = storageAccount.CreateCloudTableClient();
                     tableClient.DefaultRequestOptions.RetryPolicy = new ExponentialRetry(TimeSpan.FromSeconds(5), 5);

                     //Return only the CloudTables that are specified to be restored as per -t in commandline arguments
                     return (from tbl in tables
                                from stbl in tableClient.ListTables()
                                where stbl.Name == tbl
                                select stbl).AsEnumerable();
                 },
                 new ExecutionDataflowBlockOptions
                 {
                     MaxDegreeOfParallelism = 10,
                     BoundedCapacity = 40
                 });
                await BackupAndRestoreTableStorage.BatchAndMoveTables(storageAccount, destStorageAccount, fromAccountToTables);
            }
        }

        public static async Task RestoreTableStorageFromBlobAsync(string tablesToRestore, CloudStorageAccount storageAccount,
            CloudStorageAccount destStorageAccount)
        {
            //Specified tables to be restored
            var tables = tablesToRestore.Split(',').ToList();

            if (tables.Count > 0)
            {
                var fromAccountToContainers = new TransformManyBlock<CloudStorageAccount, CloudBlobContainer>(
                    account =>
                    {
                        var blobClient = storageAccount.CreateCloudBlobClient();
                        blobClient.DefaultRequestOptions.RetryPolicy = new ExponentialRetry(TimeSpan.FromSeconds(5), 5);

                        //only return the wanted containers to be backedup as not all system containers need to be backedup
                        return blobClient.ListContainers().Where(c =>
                        {
                            var acceptedContainer = c.Name.ToLowerInvariant();
                            return acceptedContainer.Contains("tablestoragecontainer");
                        });
                    },
                    new ExecutionDataflowBlockOptions
                    {
                        MaxDegreeOfParallelism = 10,
                        BoundedCapacity = 40
                    });

                var fromContainerToBlob = new ActionBlock<CloudBlobContainer>(cntr =>
                {
                    foreach (var table in tables)
                    {
                        var lBlobItems = cntr.ListBlobs(useFlatBlobListing: true).Cast<CloudBlob>()
                        .Where(b=>b.Name==table).ToArray();
                    }
                    

                    //var tableClient = storageAccount.CreateCloudTableClient();
                    //var table = tableClient.GetTableReference("myTable");
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
}
