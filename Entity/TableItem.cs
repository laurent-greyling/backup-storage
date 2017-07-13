using System.Collections.Generic;
using Microsoft.WindowsAzure.Storage.Table;

namespace backup_storage.Entity
{
    public class TableItem
    {
        public string TableName { get; set; }

        public IEnumerable<DynamicTableEntity> TableEntity { get; set; }
    }
}
