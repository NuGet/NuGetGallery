using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;

namespace NuGet.Services.Storage
{
    public abstract class TablePropertySerializer
    {
        public abstract void Write(string name, object value, IDictionary<string, EntityProperty> properties, OperationContext operationContext);
        public abstract object Read(Type targetType, string name, IDictionary<string, EntityProperty> properties, OperationContext operationContext);

        protected T GetOrDefault<T>(IDictionary<string, EntityProperty> dict, string name)
        {
            EntityProperty prop;
            if (!dict.TryGetValue(name, out prop))
            {
                return default(T);
            }
            return (T)prop.PropertyAsObject;
        }
    }
}
