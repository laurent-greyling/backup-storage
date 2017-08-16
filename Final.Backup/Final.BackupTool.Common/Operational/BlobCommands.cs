namespace Final.BackupTool.Common.Operational
{
    public class BlobCommands
    {
        public string ContainerName { get; set; }
        public string TableName { get; set; }
        public string BlobPath { get; set; }
        public string FromDate { get; set; }
        public string ToDate { get; set; }
        public bool Force { get; set; }
    }
}
