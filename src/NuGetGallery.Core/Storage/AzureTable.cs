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
    public class AzureTable<TEntity>
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
                namePrefix + InferTableName(typeof(TEntity)));
        }

        public Task InsertOrReplace(TEntity entity)
        {
            return _table.SafeExecute(t => t.ExecuteAsync(TableOperation.InsertOrReplace(entity)));
        }

        public async Task InsertOrIgnore(TEntity entity)
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

        public Task Merge(TEntity entity)
        {
            return _table.SafeExecute(t => t.ExecuteAsync(TableOperation.InsertOrMerge(entity)));
        }

        public async Task<TEntity> Get(string partitionKey, string rowKey)
        {
            TEntity entity;
            try
            {
                var result = await _table.ExecuteAsync(TableOperation.Retrieve<TEntity>(partitionKey, rowKey));
                if (result.HttpStatusCode != 200)
                {
                    return default(TEntity);
                }
                entity = (TEntity)result.Result;
            }
            catch (StorageException ex)
            {
                if (ex.RequestInformation != null &&
                    ex.RequestInformation.ExtendedErrorInformation != null &&
                    (ex.RequestInformation.ExtendedErrorInformation.ErrorCode == TableErrorCodeStrings.TableNotFound ||
                     ex.RequestInformation.ExtendedErrorInformation.ErrorCode == TableErrorCodeStrings.EntityNotFound))
                {
                    return default(TEntity);
                }
                throw;
            }
            return entity;
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
