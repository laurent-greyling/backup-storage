using System;

namespace Final.BackupTool.Common.Operational
{
    public class BlobOperation
    {
        public string Id { get; set; }
        public BlobOperationType OperationType { get; set; }
        public DateTimeOffset Date => DateTimeOffset.UtcNow;
        public DateTimeOffset? LastOperationDate { get; set; }

    }
}
