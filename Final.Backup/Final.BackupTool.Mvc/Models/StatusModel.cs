using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace Final.BackupTool.Mvc.Models
{
    public class StatusModel
    {
        public string PartitionKey { get; set; }
        public string Operation { get; set; }

        public DateTimeOffset? OperationDate { get; set; }
        public DateTimeOffset? EndTime { get; set; }
        public DateTimeOffset? StartTime { get; set; }
        public int TableCount { get; set; }

        public int ContainerCount { get; set; }

        public string OperationType { get; set; }

        public string TimeTaken { get; set; }

        public int Copied { get; set; }
        public int Skipped { get; set; }
        public int Faulted { get; set; }
    }
}