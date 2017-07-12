using System;
using Microsoft.WindowsAzure.Storage.Table;

namespace backup_storage.Entity
{
    public class TableStorageEntity : TableEntity
    {
        public TableStorageEntity(string partitionKey, string rowkey)
        {
            PartitionKey = partitionKey;
            RowKey = rowkey;
        }

        public TableStorageEntity()
        {
        }

        public DateTime DateOfCreation { get; set; }

        public string Name { get; set; }

        public int Age { get; set; }
    }
}
