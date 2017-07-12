using System;
using backup_storage.Entity;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;

namespace backup_storage.CreateStorage
{
    public class CreateTableStorage
    {
        /// <summary>
        /// Create and populate table storage with dummy data for testing backup and restore
        /// </summary>
        /// <param name="storageAccount"></param>
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

                //populate the table storage with some data for backing up and restoring
                var run = new TableStorageEntity($"{i}", $"myTable {id}")
                {
                    DateOfCreation = DateTime.UtcNow,
                    Name = $"name - {id}",
                    Age = i
                };

                var insertData = TableOperation.Insert(run);
                table.Execute(insertData);

                table = tableClient.GetTableReference($"myTable{i}");
                table.CreateIfNotExists();
            }
        }

        /// <summary>
        /// This is to help delete table storage to test backup
        /// This is done as there is no easy way yet to delete tables via the azure portal currently
        /// unless you have a storage manager downloaded
        /// </summary>
        /// <param name="storageAccount"></param>
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