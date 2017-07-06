using System;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Core;
using Microsoft.WindowsAzure.Storage.RetryPolicies;
using Microsoft.WindowsAzure.Storage.Table;

namespace backup_storage.Shared
{
    public class BackupTableStorage
    {
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
                    MaxDegreeOfParallelism = 4,
                    BoundedCapacity = 16
                });

            var processTables = new TransformBlock<CloudTable, ProcessingOutCome>(
                tbl =>
                {
                    try
                    {
                        var query = new TableQuery();
                        var tblData = tbl.ExecuteQuery(query);

                        var tableClientDest = destStorageAccount.CreateCloudTableClient();
                        var tble = tableClientDest.GetTableReference(tbl.Name);
                        tble.CreateIfNotExists();

                        //foreach (var dtaEntity in tblData)
                        //{
                        //    var insertDta = TableOperation.InsertOrMerge(dtaEntity);
                        //    await tble.ExecuteAsync(insertDta);
                        //}

                        Parallel.ForEach(tblData, async dtaEntity =>
                        {
                            var insertDta = TableOperation.InsertOrMerge(dtaEntity);
                            await tble.ExecuteAsync(insertDta);
                        });

                        return new ProcessingOutCome { Table = $"{tbl.Name}", Success = true };
                    }
                    catch (Exception e)
                    {
                        return new ProcessingOutCome { Table = $"{tbl.Name}", Success = false, Exception = e };
                    }
                }, new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = 4,
                    BoundedCapacity = 16
                });

            var printOutcome = new ActionBlock<ProcessingOutCome>(
                outcome =>
                {
                    if (outcome.Success)
                    {
                        Console.WriteLine($"Processed {outcome.Table}");
                    }
                    else
                    {
                        Console.Error.WriteLine($"Error while processing {outcome.Table}: {outcome.Exception}");
                    }
                });

            fromAccountToTables.LinkTo(processTables);
            processTables.LinkTo(printOutcome);

            await fromAccountToTables.SendAsync(storageAccount);
            fromAccountToTables.Complete();
            await fromAccountToTables.Completion;
            processTables.Complete();
            await processTables.Completion;
            printOutcome.Complete();
            await printOutcome.Completion;
        }
    }
    public class ProcessingOutCome
    {
        public string Table { get; set; }
        public bool Success { get; set; }
        public Exception Exception { get; set; }
    }
}