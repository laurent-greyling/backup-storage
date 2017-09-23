using System.Collections.Generic;
using System.Linq;
using Final.BackupTool.Common.Entities;
using Final.BackupTool.Common.Helpers;
using Microsoft.Azure;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;

namespace Final.BackupTool.Common.Operational
{
    public class StorageConnection
    {
        public static string ProductionStorageConnectionString { get; set; }
        public static string BackupStorageConnectionString { get; set; }
        public static string OperationStorageConnectionString { get; set; }


        private static string p => ProductionConnection();
        private static string b => BackupConnection();

        private static string o => OperationalConnection();
        private static List<ConnectionStringsEntity> Group => GetGroup();
        private static string GroupValue => CookiesReadWrite.Read(OperationalDictionary.GroupsTable, OperationalDictionary.GroupsTable);
        public static string OperationalCookie => CookiesReadWrite.Read(OperationalDictionary.OperationalCookie,
            OperationalDictionary.OperationalCookieKey);

        public StorageConnection()
        {
            ProductionStorageConnectionString = p;

            BackupStorageConnectionString = b;

            OperationStorageConnectionString = o;
        }

        private static string ProductionConnection()
        {
            var webConfig = CloudConfigurationManager.GetSetting("ProductionStorageConnectionString");
            if (!string.IsNullOrEmpty(webConfig)) return webConfig;

            if (Group==null) return OperationalDictionary.Empty;
            if (!Group.Any()) return OperationalDictionary.Empty;
            
            var productionList = Group.Select(x => x.ProductionStorageConnectionString).ToList();

            return productionList.FirstOrDefault();
        }

        private static string BackupConnection()
        {
            var webConfig = CloudConfigurationManager.GetSetting("BackupStorageConnectionString");
            if (!string.IsNullOrEmpty(webConfig)) return webConfig;

            if (Group == null) return OperationalDictionary.Empty;
            if (!Group.Any()) return OperationalDictionary.Empty;
            
            var backupList = Group.Select(x => x.BackupStorageConnectionString).ToList();

            return backupList.FirstOrDefault();
        }

        private static string OperationalConnection()
        {
            var webConfig = CloudConfigurationManager.GetSetting("OperationalStorageConnectionString");
            if (!string.IsNullOrEmpty(webConfig)) return webConfig;

            if (Group == null) return OperationalCookie;
            if (!Group.Any()) return OperationalCookie;

            var productionList = Group.Select(x => x.OperationStorageConnectionString).ToList();

            return productionList.FirstOrDefault();
        }

        public static List<ConnectionStringsEntity> GetGroup()
        {
            if (string.IsNullOrEmpty(GroupValue) || string.IsNullOrEmpty(OperationalCookie))
            {
                return new List<ConnectionStringsEntity>();
            }

            var storageAccount = CloudStorageAccount.Parse(OperationalCookie);
            var tableClient = storageAccount.CreateCloudTableClient();

            var table = tableClient.GetTableReference(OperationalDictionary.ConnectionTable);

            if (!table.Exists())
            {
                return new List<ConnectionStringsEntity>();
            }
            var operation = new TableQuery<ConnectionStringsEntity>();
            return table.ExecuteQuery(operation).Where(x => x.PartitionKey == GroupValue).ToList();
        }
    }
}
