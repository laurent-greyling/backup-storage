using System;
using System.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using backup_storage.Shared;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.RetryPolicies;
using Microsoft.WindowsAzure.Storage.Table;

namespace backup_storage.RestoreStorage
{
    public class RestoreTableStorage
    {
        public static async Task CopyAndRestoreTableStorage(string tablesToRestore, CloudStorageAccount storageAccount,
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
    }
}
