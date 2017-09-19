using Final.BackupTool.Common.Helpers;
using Microsoft.Azure;

namespace Final.BackupTool.Common.Operational
{
    public class StorageConnection
    {
        public string ProductionStorageConnectionString { get; set; }
        public string BackupStorageConnectionString { get; set; }
        public string OperationStorageConnectionString { get; set; }

        public StorageConnection()
        {
            ProductionStorageConnectionString = string.IsNullOrEmpty(CloudConfigurationManager.GetSetting("ProductionStorageConnectionString"))
                ? CookiesReadWrite.Read("production", "productionKey")
                : CloudConfigurationManager.GetSetting("ProductionStorageConnectionString");

            BackupStorageConnectionString = string.IsNullOrEmpty(CloudConfigurationManager.GetSetting("BackupStorageConnectionString"))
                ? CookiesReadWrite.Read("backup", "backupKey")
                : CloudConfigurationManager.GetSetting("BackupStorageConnectionString");

            OperationStorageConnectionString = string.IsNullOrEmpty(CloudConfigurationManager.GetSetting("OperationalStorageConnectionString"))
                ? CookiesReadWrite.Read("operational", "operationalKey")
                : CloudConfigurationManager.GetSetting("OperationalStorageConnectionString");
        }
    }
}
