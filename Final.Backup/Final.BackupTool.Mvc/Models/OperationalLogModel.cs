using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Final.BackupTool.Mvc.Models
{
    public class OperationalLogModel
    {
        public string OperationalStorageConnectionString { get; set; }
        public DateTimeOffset? LastModified { get; set; }
        public string DownloadLog { get; set; }
        public string ViewLog { get; set; }
    }
}