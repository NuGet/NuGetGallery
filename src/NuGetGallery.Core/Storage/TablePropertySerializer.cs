using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;

namespace NuGetGallery.Storage
{
    public abstract class TablePropertySerializer
    {
        public abstract void Write(string name, object value, IDictionary<string, EntityProperty> properties, OperationContext operationContext);
        public abstract object Read(Type targetType, string name, IDictionary<string, EntityProperty> properties, OperationContext operationContext);
    }
}
