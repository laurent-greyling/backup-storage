using Final.BackupTool.Common.Helpers;
using Microsoft.Azure;

namespace Final.BackupTool.Common.Operational
{
    public class StorageConnection
    {
        public string ProductionStorageConnectionString { get; set; }
        public string BackupStorageConnectionString { get; set; }
        public string OperationStorageConnectionString { get; set; }

        public string ProductionStorageCookie => CookiesReadWrite.Read("production", "productionKey");
        public string BackupStorageCookie => CookiesReadWrite.Read("backup", "backupKey");
        public string OperationStorageCookie => CookiesReadWrite.Read("operational", "operationalKey");

        public StorageConnection()
        {
            ProductionStorageConnectionString = string.IsNullOrEmpty(CloudConfigurationManager.GetSetting("ProductionStorageConnectionString"))
                ? ProductionStorageCookie
                : CloudConfigurationManager.GetSetting("ProductionStorageConnectionString");

            BackupStorageConnectionString = string.IsNullOrEmpty(CloudConfigurationManager.GetSetting("BackupStorageConnectionString"))
                ? BackupStorageCookie
                : CloudConfigurationManager.GetSetting("BackupStorageConnectionString");

            OperationStorageConnectionString = string.IsNullOrEmpty(CloudConfigurationManager.GetSetting("OperationalStorageConnectionString"))
                ? OperationStorageCookie
                : CloudConfigurationManager.GetSetting("OperationalStorageConnectionString");
        }
    }
}
