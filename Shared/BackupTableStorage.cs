using System;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.RetryPolicies;
using Microsoft.WindowsAzure.Storage.Table;

namespace backup_storage.Shared
{
    public class BackupTableStorage
    {
        /// <summary>
        /// Copy and backup table storage one at a time in parallel
        /// </summary>
        /// <param name="storageAccount"></param>
        /// <param name="destStorageAccount"></param>
        public static void CopyTableStorage(CloudStorageAccount storageAccount, CloudStorageAccount destStorageAccount)
        {
            var tableClient = storageAccount.CreateCloudTableClient();
            var tables = tableClient.ListTables();

            Parallel.ForEach(tables, (table) =>
            {
                var query = new TableQuery();
                var tblData = table.ExecuteQuery(query);

                var tableClientDest = destStorageAccount.CreateCloudTableClient();
                var tbl = tableClientDest.GetTableReference(table.Name);
                tbl.CreateIfNotExists();

                Parallel.ForEach(tblData, async dtaEntity =>
                {
                    var insertDta = TableOperation.InsertOrMerge(dtaEntity);
                    await tbl.ExecuteAsync(insertDta);
                });
            });
        }

        /// <summary>
        /// Copy and backup with dataflow - fastest option to copy tables over
        /// </summary>
        /// <param name="storageAccount"></param>
        /// <param name="destStorageAccount"></param>
        /// <returns></returns>
        public static async Task CopyAndBackUpTableStorage(CloudStorageAccount storageAccount,
            CloudStorageAccount destStorageAccount)
        {
            var fromAccountToTables = new TransformManyBlock<CloudStorageAccount, CloudTable>(
                account =>
                {
                    var tableClient = storageAccount.CreateCloudTableClient();
                    tableClient.DefaultRequestOptions.RetryPolicy = new ExponentialRetry(TimeSpan.FromSeconds(5), 5);

                    return tableClient.ListTables();
                },
                new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = 10,
                    BoundedCapacity = 40
                });

            var batchTables = new ActionBlock<CloudTable>(
                async tbl =>
                {
                    var query = new TableQuery();
                    var tblData = tbl.ExecuteQuery(query);

                    var tableClientDest = destStorageAccount.CreateCloudTableClient();
                    var tble = tableClientDest.GetTableReference(tbl.Name);
                    tble.CreateIfNotExists();
                    
                    var batchData = new BatchBlock<TableOperation>(20);
                    foreach (var dtaEntity in tblData)
                    {
                        await batchData.SendAsync(TableOperation.InsertOrMerge(dtaEntity));
                    }

                    var copyTables = new ActionBlock<TableOperation[]>(prc =>
                    {
                        var batchOp = new TableBatchOperation();

                        foreach (var pr in prc)
                        {
                            batchOp.Add(pr);
                        }

                        tble.ExecuteBatch(batchOp);
                    });

                    batchData.LinkTo(copyTables);

                    batchData.Complete();
                    await batchData.Completion;
                    copyTables.Complete();
                    await copyTables.Completion;

                    //return batchOp;
                }, new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = 10,
                    BoundedCapacity = 40
                });

            fromAccountToTables.LinkTo(batchTables);
            
            await fromAccountToTables.SendAsync(storageAccount);

            fromAccountToTables.Complete();
            await fromAccountToTables.Completion;
            batchTables.Complete();
            await batchTables.Completion;
        }
    }
}