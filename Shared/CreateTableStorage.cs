using System;
using backup_storage.Entity;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;

namespace backup_storage.Shared
{
    public class CreateTableStorage
    {
        public static void CreateAndPopulateTable(CloudStorageAccount storageAccount)
        {
            // Create the table client.
            var tableClient = storageAccount.CreateCloudTableClient();
            var table = tableClient.GetTableReference("myTable");
            // Create the first table
            table.CreateIfNotExists();

            var tables = tableClient.ListTables();
            var i = 0;

            foreach (var tbl in tables)
            {
                if (!tbl.Exists()) continue;
                i++;

                var id = Guid.NewGuid();

                var run = new TableStorageEntity($"{i}", $"myTable {id}")
                {
                    DateOfCreation = DateTime.UtcNow
                };

                var insertData = TableOperation.Insert(run);
                table.Execute(insertData);

                table = tableClient.GetTableReference($"myTable{i}");
                table.CreateIfNotExists();
            }
        }

        public static void DeleteTable(CloudStorageAccount storageAccount)
        {
            // Create the table client.
            var tableClient = storageAccount.CreateCloudTableClient();
            var table = tableClient.GetTableReference("myTable");
            table.DeleteIfExists();

            var tables = tableClient.ListTables();
            var i = 0;

            foreach (var tbl in tables)
            {
                i++;
                table = tableClient.GetTableReference($"myTable{i}");
                table.DeleteIfExists();
            }
        }
    }
}