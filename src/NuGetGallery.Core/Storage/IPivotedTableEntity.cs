using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Table;

namespace NuGetGallery.Storage
{
    /// <summary>
    /// Provides an interface to an entity that supports multiple pivots, each of which should be stored in a separate table
    /// </summary>
    public interface IPivotedTableEntity
    {
        IEnumerable<TablePivot> GetPivots();
    }

    public class TablePivot
    {
        private ITableEntity _entity;
        private Func<ITableEntity> _entityFactory;

        public string TableName { get; private set; }
        public ITableEntity Entity { 
            get { return _entity ?? (_entity = _entityFactory());}
        }

        public TablePivot(string tableName, Func<ITableEntity> entityFactory)
        {
            TableName = tableName;
            _entityFactory = entityFactory;
        }
    }

    public abstract class PivotedTableEntity : IPivotedTableEntity
    {
        private IList<Func<string, object, DynamicTableEntity, bool>> _serializers = new List<Func<string, object, DynamicTableEntity, bool>>()
        {
            EnumSerializer,
            ExceptionSerializer,
            EntityPropertySerializer
        };

        [IgnoreProperty]
        public IList<Func<string, object, DynamicTableEntity, bool>> Serializers { get { return _serializers; } }

        [IgnoreProperty]
        public DateTimeOffset Timestamp { get; set; }

        [IgnoreProperty]
        public string ReverseChronlogicalRowKey { get { return ReverseChronologicalTableEntry.GenerateReverseChronologicalKey(Timestamp); } }

        public PivotedTableEntity() : this(DateTimeOffset.UtcNow) { }
        public PivotedTableEntity(DateTimeOffset timestamp)
        {
            Timestamp = timestamp;
        }

        public abstract IEnumerable<TablePivot> GetPivots();

        protected virtual TablePivot PivotOn(Func<string> partition)
        {
            return PivotOn(String.Empty, partition, row: () => String.Empty);
        }

        protected virtual TablePivot PivotOn(Func<string> partition, Func<string> row)
        {
            return new TablePivot(String.Empty, () => GetEntity(partition(), row()));
        }

        protected virtual TablePivot PivotOn(string name, Func<string> partition)
        {
            return PivotOn(name, partition, row: () => String.Empty);
        }

        protected virtual TablePivot PivotOn(string name, Func<string> partition, Func<string> row)
        {
            return new TablePivot(name, () => GetEntity(partition(), row()));
        }

        protected virtual ITableEntity GetEntity(string partitionKey, string rowKey)
        {
            var entity = new DynamicTableEntity(partitionKey, rowKey);
            entity.Timestamp = Timestamp;

            foreach (var prop in GetCandidateProperties())
            {
                var value = prop.GetValue(this);
                if (value != null)
                {
                    foreach (var serializer in _serializers)
                    {
                        if (serializer(prop.Name, value, entity))
                        {
                            break;
                        }
                    }
                }
            }

            return entity;
        }

        protected virtual IEnumerable<PropertyInfo> GetCandidateProperties()
        {
            return from p in GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                   where p.CanRead && p.CanWrite
                   let ignore = p.GetCustomAttribute<IgnorePropertyAttribute>()
                   where ignore == null
                   select p;
        }

        private static bool EnumSerializer(string name, object value, DynamicTableEntity entity)
        {
            if (typeof(Enum).IsAssignableFrom(value.GetType()))
            {
                entity.Properties.Add(name, new EntityProperty(value.ToString()));
                entity.Properties.Add(name + "Value", new EntityProperty((int)value));
                return true;
            }
            return false;
        }

        private static bool ExceptionSerializer(string name, object value, DynamicTableEntity entity)
        {
            Exception ex = value as Exception;
            if (ex != null)
            {
                entity.Properties.Add(name + "Type", new EntityProperty(ex.GetType().FullName));
                entity.Properties.Add(name + "Message", new EntityProperty(ex.Message));
                entity.Properties.Add(name, new EntityProperty(ex.ToString()));
                return true;
            }
            return false;
        }

        private static bool EntityPropertySerializer(string name, object value, DynamicTableEntity entity)
        {
            entity.Properties.Add(name, EntityProperty.CreateEntityPropertyFromObject(value));
            return true;
        }
    }
}
