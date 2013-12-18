//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using Microsoft.WindowsAzure.Storage;
//using Microsoft.WindowsAzure.Storage.Table;

//namespace NuGet.Services.Storage
//{
//    public class PivotedAzureTable<TEntity> : AzureTable<TEntity>
//        where TEntity : PivotedTableEntity, new()
//    {
//        private Dictionary<string, AzureTable<ITableEntity>> _pivotTables = new Dictionary<string, AzureTable<ITableEntity>>(StringComparer.OrdinalIgnoreCase);
//        private TableStorageHub _tables;
//        private string _baseName;

//        public PivotedAzureTable(TableStorageHub tables)
//        {
//            _tables = tables;
//            _baseName = AzureTableHelpers.InferTableName(typeof(TEntity));
//        }

//        public override Task InsertOrReplace(TEntity entity)
//        {
//            EnsureTables(entity);
//            OnAllPivots(entity, (e, ctx, table) =>
//                table.SafeExecute(t => t.ExecuteAsync(TableOperation.InsertOrReplace(e))));
//        }

//        public override async Task InsertOrIgnore(TEntity entity)
//        {
//            EnsureTables(entity);
//            try
//            {
//                await _table.SafeExecute(t => t.ExecuteAsync(TableOperation.Insert(entity)));
//            }
//            catch (StorageException ex)
//            {
//                if (ex.RequestInformation.ExtendedErrorInformation.ErrorCode == TableErrorCodeStrings.EntityAlreadyExists)
//                {
//                    return;
//                }
//                throw;
//            }
//        }

//        public override Task Merge(TEntity entity)
//        {
//            EnsureTables(entity);
//            return _table.SafeExecute(t => t.ExecuteAsync(TableOperation.InsertOrMerge(entity)));
//        }

//        protected virtual Task OnAllPivots(TEntity entity, OperationContext context, Func<ITableEntity, OperationContext, AzureTable<ITableEntity>, Task> action)
//        {
//            // Iterate over the pivots
//            return Task.WhenAll(entity.GetPivots().Select(pivot =>
//            {
//                AzureTable<ITableEntity> table;
//                if (_pivotTables.TryGetValue(pivot.Name, out table))
//                {
//                    var entity = new DynamicTableEntity(
//                        pivot.PartitionKey,
//                        pivot.RowKey,
//                        entity.ETag,
//                        entity.WriteEntity(context))
//                        {
//                            Timestamp = entity.Timestamp
//                        };
//                    return action(entity, context, table);
//                }
//                return null;
//            }).Where(t => t != null));
//        }

//        private void EnsureTables(TEntity entity)
//        {
//            foreach (var pivot in entity.GetPivots())
//            {
//                EnsurePivotTable(pivot.Name);
//            }
//        }

//        private void EnsurePivotTable(string name)
//        {
//            if (!_pivotTables.ContainsKey(name))
//            {
//                _pivotTables[name] = _tables.Table<ITableEntity>(_baseName + name);
//            }
//        }
//    }
//}
