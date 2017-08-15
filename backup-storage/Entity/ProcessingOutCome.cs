using System;

namespace backup_storage.Entity
{
    public class ProcessingOutCome
    {
        public string Table { get; set; }
        public bool Success { get; set; }
        public Exception Exception { get; set; }
    }
}
