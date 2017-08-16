using System;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Final.BackupTool.Common.Operational
{
    public class CopyStorageOperation
    {
        public string SourceContainerName { get; set; }
        public string SourceBlobName { get; set; }
        public string SourceTableName { get; set; }
        public string SourceName => $"{SourceContainerName}/{SourceBlobName}";
        public DateTimeOffset? Snapshot { get; set; }
        public BlobType SourceBlobType { get; set; }
        public long SourceSize { get; set; }
        public DateTimeOffset? SourceBlobLastModified { get; set; }
        public string DestinationContainerName { get; set; }
        public StorageCopyStatus CopyStatus { get; set; }
        public object ExtraInformation { get; set; }
    }
}