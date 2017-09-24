using System;
using Microsoft.WindowsAzure.Storage.Table;

namespace Final.BackupTool.Common.Entities
{
    public class StorageOperationEntity : TableEntity
    {
        public string SourceAccount { get; set; }
        public string DestinationAccount { get; set; }
        public DateTimeOffset? OperationDate { get; set; }
        public DateTimeOffset? StartTime { get; set; }
        public DateTimeOffset? EndTime { get; set; }
        public string OperationType { get; set; }
        public int Copied { get; set; }
        public int Skipped { get; set; }
        public int Faulted { get; set; }
        public string ActivityType { get; set; }
    }
}
