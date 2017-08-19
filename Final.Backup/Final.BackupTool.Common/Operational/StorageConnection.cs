using System.Text.RegularExpressions;
using Microsoft.Azure;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Final.BackupTool.Common.Operational
{
    public class StorageConnection
    {
        public string ProductionStorageConnectionString => CloudConfigurationManager.GetSetting("ProductionStorageConnectionString");
        public string BackupStorageConnectionString => CloudConfigurationManager.GetSetting("BackupStorageConnectionString");
        public string OperationStorageConnectionString => CloudConfigurationManager.GetSetting("OperationalStorageConnectionString");
        public string ProductionStorageKey => GetKeyFromConnectionString(ProductionStorageConnectionString);
        public string BackupStorageKey => GetKeyFromConnectionString(BackupStorageConnectionString);
        public CloudStorageAccount ProductionStorageAccount => CloudStorageAccount.Parse(ProductionStorageConnectionString);
        public CloudStorageAccount BackupStorageAccount => CloudStorageAccount.Parse(BackupStorageConnectionString);
        public CloudStorageAccount OperationalAccount => CloudStorageAccount.Parse(OperationStorageConnectionString);
        public CloudBlobClient ProductionBlobClient => ProductionStorageAccount.CreateCloudBlobClient();
        public CloudBlobClient BackupBlobClient => BackupStorageAccount.CreateCloudBlobClient();

        private static string GetKeyFromConnectionString(string connectionString)
        {
            var regPattern = new Regex(@"(?<=AccountKey=)(.*)(?=;)", RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace);
            return regPattern.Matches(connectionString)[0].Value;
        }
    }
}
