using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace Final.BackupTool.WebSite.Model
{
    public class ConnectionStringModel
    {
        [Required]
        public string ProductionStorageConnectionString { get; set; }
        [Required]
        public string BackupStorageConnectionString { get; set; }
        [Required]
        public string OperationalStorageConnectionString { get; set; }
    }
}
