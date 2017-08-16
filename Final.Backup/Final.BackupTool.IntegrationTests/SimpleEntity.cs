using Microsoft.WindowsAzure.Storage.Table;

namespace Final.BackupTool.IntegrationTests
{
    public class SimpleEntity : TableEntity
    {
        public SimpleEntity(string partitionKey, string rowKey, string value)
        {
            PartitionKey = partitionKey;
            RowKey = rowKey;
            Value = value;
        }

        public SimpleEntity() { }

        public string Value { get; set; }
    }
}

