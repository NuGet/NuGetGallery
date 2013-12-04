using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;

namespace NuGetGallery.Storage
{
    /// <summary>
    /// A better base class for azure table entities
    /// </summary>
    public abstract class AzureTableEntity : ITableEntity
    {
        public virtual DateTimeOffset Timestamp { get; set; }
        public virtual string PartitionKey { get; set; }
        public virtual string RowKey { get; set; }
        public virtual string ETag { get; set; }

        protected AzureTableEntity() { }
        protected AzureTableEntity(string partitionKey, string rowKey, DateTimeOffset timestamp)
        {
            PartitionKey = partitionKey;
            RowKey = rowKey;
            Timestamp = timestamp;
        }

        public virtual IDictionary<string, EntityProperty> WriteEntity(OperationContext operationContext)
        {
            RefreshKeys();
            Dictionary<string, EntityProperty> properties = new Dictionary<string,EntityProperty>();
            foreach (var property in GetProperties())
            {
                // Get the value
                var value = property.Getter();

                // Serialize the value
                property.Serializer.Write(property.PropertyName, value, properties, operationContext);
            }
            return properties;
        }

        public virtual void ReadEntity(IDictionary<string, EntityProperty> properties, OperationContext operationContext)
        {
            foreach (var property in GetProperties())
            {
                // Deserialize the value
                var value = property.Serializer.Read(property.Type, property.PropertyName, properties, operationContext);
                
                // Set the value
                property.Setter(value);
            }
            RefreshKeys();
        }

        protected virtual IEnumerable<TableProperty> GetProperties()
        {
            return SelectProperties().Select(CreateTableProperty);
        }

        protected virtual TableProperty CreateTableProperty(PropertyInfo property)
        {
            return new TableProperty(this, property);
        }

        protected virtual IEnumerable<PropertyInfo> SelectProperties()
        {
            return from p in GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                   where p.CanRead && p.CanWrite
                   let ignore = p.GetCustomAttribute<IgnorePropertyAttribute>()
                   where ignore == null
                   select p;
        }

        protected virtual void RefreshKeys()
        {

        }
    }
}
