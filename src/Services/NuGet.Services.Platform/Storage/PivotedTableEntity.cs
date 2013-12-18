//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace NuGet.Services.Storage
//{
//    public abstract class PivotedTableEntity : AzureTableEntity
//    {
//        protected internal virtual IEnumerable<TablePivot> GetPivots()
//        {
//            return Enumerable.Empty<TablePivot>();
//        }
//    }

//    public class TablePivot {
//        public string Name { get; private set; }
//        public string PartitionKey { get; private set; }
//        public string RowKey { get; private set; }

//        public TablePivot(string name, string partitionKey, string rowKey) {
//            Name = name;
//            PartitionKey = partitionKey;
//            RowKey = rowKey;
//        }
//    }
//}
