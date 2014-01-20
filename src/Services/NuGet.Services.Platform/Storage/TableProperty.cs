using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.Storage
{
    public class TableProperty
    {
        public string PropertyName { get; private set; }
        public TablePropertySerializer Serializer { get; private set; }
        public Type Type { get; set; }
        public Func<object> Getter { get; private set; }
        public Action<object> Setter { get; private set; }

        public TableProperty(string propertyName, TablePropertySerializer serializer, Type type, Func<object> getter, Action<object> setter)
        {
            PropertyName = propertyName;
            Serializer = serializer;
            Type = type;
            Getter = getter;
            Setter = setter;
        }

        public TableProperty(object instance, PropertyInfo property)
        {
            // PERF: This is very slow. We can totally do better!

            var attributes = property.GetCustomAttributes();
            var columnAttribute = attributes.OfType<ColumnAttribute>().FirstOrDefault();
            var converterAttribute = attributes.OfType<TypeConverterAttribute>().FirstOrDefault();
            var serializerAttribute = attributes.OfType<PropertySerializerAttribute>().FirstOrDefault();

            if (converterAttribute == null || serializerAttribute == null)
            {
                var typeAttributes = property.PropertyType.GetCustomAttributes();
                converterAttribute = converterAttribute ?? typeAttributes.OfType<TypeConverterAttribute>().FirstOrDefault();
                serializerAttribute = serializerAttribute ?? typeAttributes.OfType<PropertySerializerAttribute>().FirstOrDefault();
            }

            Type = property.PropertyType;
            PropertyName = columnAttribute == null ? property.Name : columnAttribute.Name;

            // Some special cases
            if (property.PropertyType == typeof(DateTimeOffset?))
            {
                Getter = () =>
                {
                    var val = ((DateTimeOffset?)property.GetValue(instance));
                    return val.HasValue ? val.Value.UtcDateTime : (DateTimeOffset?)null;
                };
                Setter = value =>
                {
                    if (value != null)
                    {
                        DateTimeOffset val;
                        if (value is DateTime)
                        {
                            val = new DateTimeOffset(((DateTime)value), TimeSpan.Zero);
                        }
                        else
                        {
                            val = (DateTimeOffset)value;
                        }
                        property.SetValue(instance, val);
                    }
                };
            }
            else
            {
                Getter = () => property.GetValue(instance);
                Setter = value => property.SetValue(instance, value);
            }

            if (serializerAttribute != null)
            {
                Serializer = (TablePropertySerializer)Activator.CreateInstance(serializerAttribute.Type);
            }
            else
            {
                TypeConverter converter = null;
                if (converterAttribute != null)
                {
                    Type typ = Type.GetType(converterAttribute.ConverterTypeName);
                    if (typ != null)
                    {
                        converter = Activator.CreateInstance(typ) as TypeConverter;
                    }
                }

                Serializer = new DefaultTablePropertySerializer(converter);
            }
        }
    }
}
