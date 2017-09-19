using System;

namespace Final.BackupTool.Common.Models
{
    public class OperationalLogModel
    {
        public string OperationalStorageConnectionString { get; set; }
        public DateTimeOffset? LastModified { get; set; }
        public string DownloadLog { get; set; }
        public string ViewLog { get; set; }
    }
}