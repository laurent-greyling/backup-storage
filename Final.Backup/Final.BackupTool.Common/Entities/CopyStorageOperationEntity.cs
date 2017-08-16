using Microsoft.WindowsAzure.Storage.Table;

namespace Final.BackupTool.Common.Entities
{
    public class CopyStorageOperationEntity : TableEntity
    {
        public string Source { get; set; }

        public string Status { get; set; }

        public string ExtraInformation { get; set; }
    }
}
