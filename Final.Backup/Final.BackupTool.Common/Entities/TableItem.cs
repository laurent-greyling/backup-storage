using System.Collections.Generic;
using Microsoft.WindowsAzure.Storage.Table;

namespace Final.BackupTool.Common.Entities
{
    public class TableItem
    {
        public string TableName { get; set; }

        public IEnumerable<DynamicTableEntity> TableEntity { get; set; }
    }
}
