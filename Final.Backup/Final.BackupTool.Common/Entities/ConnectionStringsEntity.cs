using Microsoft.WindowsAzure.Storage.Table;

namespace Final.BackupTool.Common.Entities
{
    public class ConnectionStringsEntity : TableEntity
    {
        public string ProductionStorageConnectionString { get; set; }
        public string BackupStorageConnectionString { get; set; }
        public string OperationStorageConnectionString { get; set; }
    }
}
