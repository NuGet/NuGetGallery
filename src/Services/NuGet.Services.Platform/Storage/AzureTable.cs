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

namespace NuGet.Services.Storage
{
    public class AzureTable<TEntity>
        where TEntity : ITableEntity, new()
    {
        private CloudTable _table;

        public AzureTable(CloudTable table)
        {
            _table = table;
        }

        public AzureTable(CloudTableClient client, string namePrefix)
        {
            _table = client.GetTableReference(
                namePrefix + AzureTableHelpers.InferTableName(typeof(TEntity)));
        }

        public virtual Task InsertOrReplace(TEntity entity)
        {
            return _table.SafeExecute(t => t.ExecuteAsync(TableOperation.InsertOrReplace(entity)));
        }

        public virtual async Task InsertOrIgnore(TEntity entity)
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

        public virtual Task Merge(TEntity entity)
        {
            return _table.SafeExecute(t => t.ExecuteAsync(TableOperation.InsertOrMerge(entity)));
        }

        public virtual async Task<TEntity> Get(string partitionKey, string rowKey)
        {
            return await SafeExecuteWithoutCreate(async table =>
            {
                var result = await table.ExecuteAsync(TableOperation.Retrieve<TEntity>(partitionKey, rowKey));
                if (result.HttpStatusCode != 200)
                {
                    return default(TEntity);
                }
                return (TEntity)result.Result;
            });
        }

        public virtual IEnumerable<TEntity> Get(string partitionKey)
        {
            return Query(
                _table.CreateQuery<TEntity>()
                    .Where(t => t.PartitionKey == partitionKey));
        }

        public virtual IEnumerable<TEntity> GetAll()
        {
            return Query(_table.CreateQuery<TEntity>());    
        }

        protected virtual IEnumerable<TEntity> Query(IQueryable<TEntity> query) 
        {
            var enumerator = query.GetEnumerator();

            // There may be an exception grabbing the first one due to the table being missing
            try
            {
                if (!enumerator.MoveNext())
                {
                    yield break;
                }
            }
            catch (StorageException ex)
            {
                if (ex.RequestInformation != null &&
                    ex.RequestInformation.ExtendedErrorInformation != null &&
                    (ex.RequestInformation.ExtendedErrorInformation.ErrorCode == TableErrorCodeStrings.TableNotFound ||
                     ex.RequestInformation.ExtendedErrorInformation.ErrorCode == TableErrorCodeStrings.EntityNotFound))
                {
                    yield break; // Just return nothing, the table doesn't exist yet.
                }
                throw;
            }

            do
            {
                yield return enumerator.Current;
            } while (enumerator.MoveNext());
        }

        private async Task<TResult> SafeExecuteWithoutCreate<TResult>(Func<CloudTable, Task<TResult>> action)
        {
            TResult result;
            try
            {
                result = await action(_table);
            }
            catch (StorageException ex)
            {
                if (ex.RequestInformation != null &&
                    ex.RequestInformation.ExtendedErrorInformation != null &&
                    (ex.RequestInformation.ExtendedErrorInformation.ErrorCode == TableErrorCodeStrings.TableNotFound ||
                     ex.RequestInformation.ExtendedErrorInformation.ErrorCode == TableErrorCodeStrings.EntityNotFound))
                {
                    return default(TResult);
                }
                throw;
            }
            return result;
        }
    }

    internal static class AzureTableHelpers
    {
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
