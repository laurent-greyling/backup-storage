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
        public string ProductionStorageConnectionString { get; set; }
        public string BackupStorageConnectionString { get; set; }
        public string OperationStorageConnectionString { get; set; }


        public IEnumerable<ConnectionStringsEntity> Group => GetGroup();
        public StorageConnection()
        {
            ProductionStorageConnectionString =
                string.IsNullOrEmpty(CloudConfigurationManager.GetSetting("ProductionStorageConnectionString"))
                    ? !Group.Any()
                        ? ""
                        : Group.Select(x => x.ProductionStorageConnectionString).ToList()[0]
                    : CloudConfigurationManager.GetSetting("ProductionStorageConnectionString");

            BackupStorageConnectionString = string.IsNullOrEmpty(CloudConfigurationManager.GetSetting("BackupStorageConnectionString"))
                ? !Group.Any()
                    ? ""
                    : Group.Select(x => x.BackupStorageConnectionString).ToList()[0]
                : CloudConfigurationManager.GetSetting("BackupStorageConnectionString");

            OperationStorageConnectionString = string.IsNullOrEmpty(CloudConfigurationManager.GetSetting("OperationalStorageConnectionString"))
                ? !Group.Any()
                    ? CookiesReadWrite.Read(OperationalDictionary.OperationalCookie,
                        OperationalDictionary.OperationalCookieKey)
                    : Group.Select(x => x.OperationStorageConnectionString).ToList()[0]
                : CloudConfigurationManager.GetSetting("OperationalStorageConnectionString");
        }

        private static List<ConnectionStringsEntity> GetGroup()
        {
            var groupValue = CookiesReadWrite.Read(OperationalDictionary.GroupsTable, OperationalDictionary.GroupsTable);
            var operationalStorage = CookiesReadWrite.Read(OperationalDictionary.OperationalCookie,
                OperationalDictionary.OperationalCookieKey);

            if (string.IsNullOrEmpty(groupValue) || string.IsNullOrEmpty(operationalStorage))
            {
                return new List<ConnectionStringsEntity>();
            }

            var storageAccount = CloudStorageAccount.Parse(operationalStorage);
            var tableClient = storageAccount.CreateCloudTableClient();

            var table = tableClient.GetTableReference(OperationalDictionary.ConnectionTable);

            if (!table.Exists())
            {
                return new List<ConnectionStringsEntity>();
            }
            var operation = new TableQuery<ConnectionStringsEntity>();
            return table.ExecuteQuery(operation).Where(x => x.PartitionKey == groupValue).ToList();
        }
    }
}
