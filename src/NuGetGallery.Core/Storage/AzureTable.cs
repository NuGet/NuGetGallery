using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using Microsoft.WindowsAzure.Storage.Table.Protocol;

namespace NuGetGallery.Monitoring
{
    public class MonitoringTable<TEntity> where TEntity : ITableEntity
    {
        private CloudTableClient _client;
        private CloudTable _table;
        private string _name;
        private string _namePrefix;

        public virtual string TableName
        {
            get { return _name ?? (_name = InferTableName(typeof(TEntity))); }
        }

        public virtual CloudTable Table
        {
            get { return _table ?? (_table = _client.GetTableReference(TableName)); }
        }

        protected MonitoringTable(CloudTableClient client)
        {
            _client = client;
        }

        public MonitoringTable(CloudTableClient client, string namePrefix)
            : this(client)
        {
            _namePrefix = namePrefix;
        }

        /// <summary>
        /// Inserts the specified value or replaces an existing value if one exists with the same keys
        /// </summary>
        public Task Upsert(TEntity entity)
        {
            return _table.SafeExecute(t => t.ExecuteAsync(TableOperation.InsertOrReplace(entity)));
        }

        /// <summary>
        /// Inserts the specified value and silently fails, ignoring the new value, if there is already an entry with the same keys
        /// </summary>
        public async Task InsertOrIgnoreDuplicate(TEntity entity)
        {
            try
            {
                await _table.SafeExecute(t => t.ExecuteAsync(TableOperation.Insert(entity)));
            }
            catch (StorageException ex)
            {
                if (ex.RequestInformation.ExtendedErrorInformation.ErrorCode == TableErrorCodeStrings.EntityAlreadyExists)
                {
                    return;
                }
                throw;
            }
        }

        private static ConcurrentDictionary<Type, string> _tableNameMap = new ConcurrentDictionary<Type, string>();
        private static string InferTableName(Type entityType)
        {
            return _tableNameMap.GetOrAdd(entityType, t =>
            {
                string name = t.Name;
                TableAttribute attr = t.GetCustomAttribute<TableAttribute>();
                if (attr != null)
                {
                    name = attr.Name;
                }
                else
                {
                    if (name.EndsWith("Entry"))
                    {
                        name = name.Substring(0, name.Length - 5);
                    }
                }
                return (_namePrefix ?? String.Empty) + name;
            });
        }
    }
}
