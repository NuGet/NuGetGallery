using System;
using System.Reflection;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Table;
using Microsoft.WindowsAzure.Storage.Table.Protocol;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.WindowsAzure.Storage;

namespace NuGetGallery.Storage
{
    public class AzureTable<TEntity> : IWriteOnlyTable<TEntity>
        where TEntity : ITableEntity
    {
        private CloudTable _table;

        public AzureTable(CloudTable table)
        {
            _table = table;
        }

        public AzureTable(CloudTableClient client, string namePrefix)
        {
            _table = client.GetTableReference(
                namePrefix + AzureTableHelper.InferTableName(typeof(TEntity)));
        }

        public Task Upsert(TEntity entity)
        {
            return AzureTableHelper.Upsert(_table, entity);
        }

        public Task InsertOrIgnore(TEntity entity)
        {
            return AzureTableHelper.InsertOrIgnore(_table, entity);   
        }

        public Task Merge(TEntity entity)
        {
            return AzureTableHelper.Merge(_table, entity);
        }
    }

    internal static class AzureTableHelper
    {
        public static Task Upsert(CloudTable table, ITableEntity entity)
        {
            return table.SafeExecute(t => t.ExecuteAsync(TableOperation.InsertOrReplace(entity)));
        }

        public static async Task InsertOrIgnore(CloudTable table, ITableEntity entity)
        {
            try
            {
                await table.SafeExecute(t => t.ExecuteAsync(TableOperation.Insert(entity)));
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

        public static Task Merge(CloudTable table, ITableEntity entity)
        {
            return table.SafeExecute(t => t.ExecuteAsync(TableOperation.InsertOrMerge(entity)));
        }

        private static ConcurrentDictionary<Type, string> _tableNameMap = new ConcurrentDictionary<Type, string>();
        public static string InferTableName(Type entityType)
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
                return name;
            });
        }
    }
}
