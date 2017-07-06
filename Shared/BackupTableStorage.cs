using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
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

                Parallel.ForEach(tblData, async dtaEntity =>
                {
                    var insertDta = TableOperation.InsertOrMerge(dtaEntity);
                    await tbl.ExecuteAsync(insertDta);
                });
            });
        }
    }
}