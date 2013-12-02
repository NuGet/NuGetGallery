using System;
using System.Reflection;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using Microsoft.WindowsAzure.Storage.Table.Protocol;

namespace NuGetGallery.Storage
{
    public class PivotedTable<TEntity> : IWriteOnlyTable<TEntity>
        where TEntity : IPivotedTableEntity
    {
        private string _namePrefix;
        private CloudTableClient _client;
        private string _baseName;

        private ConcurrentDictionary<string, CloudTable> _pivotedTables = new ConcurrentDictionary<string,CloudTable>(StringComparer.OrdinalIgnoreCase);

        public virtual string BaseName
        {
            get { return _baseName ?? (_baseName = AzureTableHelper.InferTableName(typeof(TEntity))); }
        }

        public PivotedTable(CloudTableClient client, string namePrefix)
        {
            _client = client;
            _namePrefix = namePrefix;
        }

        /// <summary>
        /// Inserts the specified value or replaces an existing value if one exists with the same keys
        /// </summary>
        public virtual Task Upsert(TEntity entity)
        {
            return ForEachPivot(entity, AzureTableHelper.Upsert);
        }

        /// <summary>
        /// Inserts the specified value and silently fails, ignoring the new value, if there is already an entry with the same keys
        /// </summary>
        public virtual Task InsertOrIgnore(TEntity entity)
        {
            return ForEachPivot(entity, AzureTableHelper.InsertOrIgnore);
        }

        public Task Merge(TEntity entity)
        {
            return ForEachPivot(entity, AzureTableHelper.Merge);
        }

        private Task ForEachPivot(TEntity entity, Func<CloudTable, ITableEntity, Task> action)
        {
            return Task.WhenAll(
                entity.GetPivots().Select(p =>
                {
                    var table = _pivotedTables.GetOrAdd(GetFullTableName(p), name => _client.GetTableReference(name));
                    return action(table, p.Entity);
                }));
        }

        private string GetFullTableName(TablePivot pivot)
        {
            return _namePrefix + BaseName + pivot.TableName;
        }
    }
}
