using System;
using System.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using backup_storage.Entity;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Core;
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

                //var batchOp = new TableBatchOperation();

                //Parallel.ForEach(tblData, (dtaEntity) =>
                //{
                //    batchOp.Add(TableOperation.InsertOrMerge(dtaEntity));
                //});

                //tbl.ExecuteBatch(batchOp);

                Parallel.ForEach(tblData, async dtaEntity =>
                {
                    var insertDta = TableOperation.InsertOrMerge(dtaEntity);
                    await tbl.ExecuteAsync(insertDta);
                });
            });
        }

        /// <summary>
        /// Copy and backup with dataflow
        /// </summary>
        /// <param name="storageAccount"></param>
        /// <param name="destStorageAccount"></param>
        /// <returns></returns>
        public static async Task CopyAndBackUpTableStorage(CloudStorageAccount storageAccount,
            CloudStorageAccount destStorageAccount)
        {
            CloudTable tble = null;
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

            var batchTables = new TransformBlock<CloudTable, TableBatchOperation>(
                tbl =>
                {
                    var query = new TableQuery();
                    var tblData = tbl.ExecuteQuery(query);

                    var tableClientDest = destStorageAccount.CreateCloudTableClient();
                    tble = tableClientDest.GetTableReference(tbl.Name);
                    tble.CreateIfNotExists();

                    var batchOp = new TableBatchOperation();

                    Parallel.ForEach(tblData, dtaEntity =>
                    {
                        batchOp.Add(TableOperation.InsertOrMerge(dtaEntity));
                    });

                    return batchOp;
                }, new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = 10,
                    BoundedCapacity = 40
                });


            var batchData = new BatchBlock<TableBatchOperation>(20);

            var copyTables = new ActionBlock<TableBatchOperation[]>(prc =>
            {
                foreach (var pr in prc)
                {
                    tble.ExecuteBatch(pr);
                }
            });
            //var printOutcome = new ActionBlock<ProcessingOutCome>(
            //    outcome =>
            //    {
            //        if (outcome.Success)
            //        {
            //            Console.WriteLine($"Processed {outcome.Table}");
            //        }
            //        else
            //        {
            //            Console.Error.WriteLine($"Error while processing {outcome.Table}: {outcome.Exception}");
            //        }
            //    });

            fromAccountToTables.LinkTo(batchTables);
            batchTables.LinkTo(batchData);
            batchData.LinkTo(copyTables);


            //processTables.LinkTo(printOutcome);

            await fromAccountToTables.SendAsync(storageAccount);

            fromAccountToTables.Complete();
            await fromAccountToTables.Completion;
            batchTables.Complete();
            await batchTables.Completion;

            await batchData.Completion.ContinueWith(delegate { copyTables.Complete(); });
            batchData.Complete();
            
            
            //printOutcome.Complete();
            //await printOutcome.Completion;
        }
    }
}