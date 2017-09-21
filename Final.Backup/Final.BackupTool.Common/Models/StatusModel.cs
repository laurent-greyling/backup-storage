using System;

namespace Final.BackupTool.Common.Models
{
    public class StatusModel
    {
        public string OperationalStorageConnectionString { get; set; }
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
        public bool BackupTable { get; set; }
        public bool BackupBlobs { get; set; }
        public bool RestoreTable { get; set; }
        public bool RestoreBlobs { get; set; }
    }
}