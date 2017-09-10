using Microsoft.Azure;

namespace Final.BackupTool.Common.Operational
{
    public class StorageConnection
    {
        public string ProductionStorageConnectionString => CloudConfigurationManager.GetSetting("ProductionStorageConnectionString");
        public string BackupStorageConnectionString => CloudConfigurationManager.GetSetting("BackupStorageConnectionString");
        public string OperationStorageConnectionString => CloudConfigurationManager.GetSetting("OperationalStorageConnectionString");
    }
}
