using System.Collections.Generic;

namespace Final.BackupTool.Common.Models
{
    public class LatestStatusModel
    {
        public string ConnectionString { get; set; }
        public List<StatusModel> Status { get; set; }
    }
}
