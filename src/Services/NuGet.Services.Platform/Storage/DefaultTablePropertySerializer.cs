using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;

namespace NuGet.Services.Storage
{
    public class DefaultTablePropertySerializer : TablePropertySerializer
    {
        private TypeConverter _converter;
        private AssemblyInformationPropertySerializer _asmInfoSerializer = new AssemblyInformationPropertySerializer();

        public DefaultTablePropertySerializer(TypeConverter converter)
        {
            _converter = converter;
        }

        public override void Write(string name, object value, IDictionary<string, EntityProperty> properties, OperationContext operationContext)
        {
            if (_converter != null && _converter.CanConvertTo(typeof(string)))
            {
                properties[name] = new EntityProperty(_converter.ConvertToString(value));
            }
            else if (value is AssemblyInformation)
            {
                _asmInfoSerializer.Write(name, value, properties, operationContext);
            }
            else if (value != null && value.GetType().IsEnum)
            {
                properties[name] = EntityProperty.GeneratePropertyForString(value.ToString());
            }
            else if (value != null)
            {
                properties[name] = EntityProperty.CreateEntityPropertyFromObject(value);
            }
        }

        public override object Read(Type targetType, string name, IDictionary<string, EntityProperty> properties, OperationContext operationContext)
        {
            if (targetType == typeof(AssemblyInformation))
            {
                return _asmInfoSerializer.Read(targetType, name, properties, operationContext);
            }
            else
            {
                EntityProperty prop;
                if (properties.TryGetValue(name, out prop))
                {
                    var val = properties[name];
                    if (targetType.IsEnum && val.PropertyType == EdmType.String)
                    {
                        return Enum.Parse(targetType, val.StringValue);
                    }
                    else if (val.PropertyType == EdmType.String && _converter != null && _converter.CanConvertFrom(typeof(string)))
                    {
                        return _converter.ConvertFromString(val.StringValue);
                    }
                    else if (val.PropertyType == EdmType.DateTime && targetType == typeof(DateTimeOffset))
                    {
                        return val.DateTimeOffsetValue.Value;
                    }
                    else
                    {
                        return val.PropertyAsObject;
                    }
                }
                return null;
            }
        }
    }
}
