using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using Xunit;

namespace NuGet.Services.Storage
{
    public class AzureTableEntityFacts
    {
        public class Serialization
        {
            [Fact]
            public void SerializesBuiltInValues()
            {
                // Arrange
                var obj = new TestObject()
                {
                    Foo = "foo",
                    Bar = 42,
                    Enum = StringComparison.Ordinal,
                    Date = new DateTimeOffset(2013, 1, 1, 1, 1, 1, TimeSpan.Zero),
                    Nullable = true
                };

                // Act
                var serialized = obj.WriteEntity(new OperationContext());

                // Assert
                var expected = new Dictionary<string, EntityProperty>()
                {
                    { "Foo", EntityProperty.GeneratePropertyForString(obj.Foo) },
                    { "Bar", EntityProperty.GeneratePropertyForInt(obj.Bar) },
                    { "Enum", EntityProperty.GeneratePropertyForString(obj.Enum.ToString()) },
                    { "Date", EntityProperty.GeneratePropertyForDateTimeOffset(obj.Date) },
                    { "Nullable", EntityProperty.GeneratePropertyForBool(obj.Nullable.Value) },
                };
                Assert.Equal(expected.Count, Enumerable.Intersect(expected, serialized).Count());
            }

            [Fact]
            public void DoesNotSerializeNullNullable()
            {
                // Arrange
                var obj = new TestObject()
                {
                    Foo = "foo",
                    Nullable = null
                };

                // Act
                var serialized = obj.WriteEntity(new OperationContext());

                // Assert
                var expected = new Dictionary<string, EntityProperty>()
                {
                    { "Foo", EntityProperty.GeneratePropertyForString(obj.Foo) }
                };
                Assert.Equal(expected.Count, Enumerable.Intersect(expected, serialized).Count());
            }

            [Fact]
            public void UsesTypeConverterFromType()
            {
                // Arrange
                var obj = new TestObject()
                {
                    TypeConverter = new Uri("http://microsoft.com")
                };

                // Act
                var serialized = obj.WriteEntity(new OperationContext());

                // Assert
                var expected = new Dictionary<string, EntityProperty>()
                {
                    { "TypeConverter", EntityProperty.GeneratePropertyForString(obj.TypeConverter.OriginalString) }
                };
                Assert.Equal(expected.Count, Enumerable.Intersect(expected, serialized).Count());
            }

            [Fact]
            public void UsesTypeConverterFromProperty()
            {
                // Arrange
                var obj = new TestObject()
                {
                    PropertyConverter = StringComparison.CurrentCulture
                };

                // Act
                var serialized = obj.WriteEntity(new OperationContext());

                // Assert
                var expected = new Dictionary<string, EntityProperty>()
                {
                    { "PropertyConverter", EntityProperty.GeneratePropertyForString(((int)obj.PropertyConverter).ToString()) }
                };
                Assert.Equal(expected.Count, Enumerable.Intersect(expected, serialized).Count());
            }

            [Fact]
            public void UsesSerializerFromType()
            {
                // Arrange
                var obj = new TestObject()
                {
                    TypeSerializer = new SubObject()
                    {
                        A = 42,
                        B = false
                    }
                };

                // Act
                var serialized = obj.WriteEntity(new OperationContext());

                // Assert
                var expected = new Dictionary<string, EntityProperty>()
                {
                    { "TypeSerializer.A", EntityProperty.GeneratePropertyForInt(obj.TypeSerializer.A) },
                    { "TypeSerializer.B", EntityProperty.GeneratePropertyForBool(obj.TypeSerializer.B) }
                };
                Assert.Equal(expected.Count, Enumerable.Intersect(expected, serialized).Count());
            }

            [Fact]
            public void UsesSerializerFromProperty()
            {
                // Arrange
                var obj = new TestObject()
                {
                    PropertySerializer = "aBc"
                };

                // Act
                var serialized = obj.WriteEntity(new OperationContext());

                // Assert
                var expected = new Dictionary<string, EntityProperty>()
                {
                    { "PropertySerializer.Upper", EntityProperty.GeneratePropertyForString("ABC") },
                    { "PropertySerializer.Lower", EntityProperty.GeneratePropertyForString("abc") },
                    { "PropertySerializer", EntityProperty.GeneratePropertyForString("aBc") }
                };
                Assert.Equal(expected.Count, Enumerable.Intersect(expected, serialized).Count());
            }
        }

        public class Deserialization
        {
            [Fact]
            public void DeserializesBuiltInValues()
            {
                // Arrange
                var properties = new Dictionary<string, EntityProperty>()
                {
                    { "Foo", EntityProperty.GeneratePropertyForString("foo") },
                    { "Bar", EntityProperty.GeneratePropertyForInt(42) },
                    { "Enum", EntityProperty.GeneratePropertyForString("Ordinal") },
                    { "Date", EntityProperty.GeneratePropertyForDateTimeOffset(new DateTimeOffset(2013, 1, 1, 1, 1, 1, TimeSpan.Zero)) },
                    { "Nullable", EntityProperty.GeneratePropertyForBool(true) },
                };
                
                // Act
                var deserialized = new TestObject();
                deserialized.ReadEntity(properties, new OperationContext());

                // Assert
                var expected = new TestObject()
                {
                    Foo = "foo",
                    Bar = 42,
                    Enum = StringComparison.Ordinal,
                    Date = new DateTimeOffset(2013, 1, 1, 1, 1, 1, TimeSpan.Zero),
                    Nullable = true
                };
                Assert.Equal(expected, deserialized);
            }

            [Fact]
            public void UsesTypeConverterFromType()
            {
                // Arrange
                var properties = new Dictionary<string, EntityProperty>()
                {
                    { "TypeConverter", EntityProperty.GeneratePropertyForString("http://microsoft.com") }
                };

                // Act
                var deserialized = new TestObject();
                deserialized.ReadEntity(properties, new OperationContext());

                // Assert
                var expected = new TestObject()
                {
                    TypeConverter = new Uri("http://microsoft.com")
                };
                Assert.Equal(expected, deserialized);
            }

            [Fact]
            public void UsesTypeConverterFromProperty()
            {
                // Arrange
                var properties = new Dictionary<string, EntityProperty>()
                {
                    { "PropertyConverter", EntityProperty.GeneratePropertyForString(((int)StringComparison.CurrentCulture).ToString()) }
                };

                // Act
                var deserialized = new TestObject();
                deserialized.ReadEntity(properties, new OperationContext());

                // Assert
                var expected = new TestObject()
                {
                    PropertyConverter = StringComparison.CurrentCulture
                }; 
                Assert.Equal(expected, deserialized);
            }

            [Fact]
            public void UsesSerializerFromType()
            {
                // Arrange
                var properties = new Dictionary<string, EntityProperty>()
                {
                    { "TypeSerializer.A", EntityProperty.GeneratePropertyForInt(42) },
                    { "TypeSerializer.B", EntityProperty.GeneratePropertyForBool(false) }
                };

                // Act
                var deserialized = new TestObject();
                deserialized.ReadEntity(properties, new OperationContext());

                // Assert
                var expected = new TestObject()
                {
                    TypeSerializer = new SubObject()
                    {
                        A = 42,
                        B = false
                    }
                };
                Assert.Equal(expected, deserialized);
            }

            [Fact]
            public void UsesSerializerFromProperty()
            {
                // Arrange
                var properties = new Dictionary<string, EntityProperty>()
                {
                    { "PropertySerializer.Upper", EntityProperty.GeneratePropertyForString("ABC") },
                    { "PropertySerializer.Lower", EntityProperty.GeneratePropertyForString("abc") },
                    { "PropertySerializer", EntityProperty.GeneratePropertyForString("aBc") }
                };

                // Act
                var deserialized = new TestObject();
                deserialized.ReadEntity(properties, new OperationContext());

                // Assert
                var expected = new TestObject()
                {
                    PropertySerializer = "aBc"
                };
                Assert.Equal(expected, deserialized);
            }
        }

        public class TestObject : AzureTableEntity
        {
            public string Foo { get; set; }
            public int Bar { get; set; }
            public StringComparison Enum { get; set; }
            public DateTimeOffset Date { get; set; }
            public bool? Nullable { get; set; }

            public Uri TypeConverter { get; set; }

            [TypeConverter(typeof(IntEnumTypeConverter))]
            public StringComparison PropertyConverter { get; set; }

            public SubObject TypeSerializer { get; set; }

            [PropertySerializerAttribute(typeof(MultiCaseSerializer))]
            public string PropertySerializer { get; set; }

            public override bool Equals(object obj)
            {
                var other = obj as TestObject;
                return other != null &&
                    Equals(other.Foo, Foo) &&
                    Equals(other.Bar, Bar) &&
                    Equals(other.Date, Date) &&
                    Equals(other.Nullable, Nullable) &&
                    Equals(other.TypeConverter, TypeConverter) &&
                    Equals(other.PropertyConverter, PropertyConverter) &&
                    Equals(other.TypeSerializer, TypeSerializer) &&
                    Equals(other.PropertySerializer, PropertySerializer);
            }

            public override int GetHashCode()
            {
                return base.GetHashCode();
            }
        }

        [PropertySerializer(typeof(SubObjectSerializer))]
        public class SubObject
        {
            public int A { get; set; }
            public bool B { get; set; }

            public override bool Equals(object obj)
            {
                var other = (SubObject)obj;
                return other.A == A && other.B == B;
            }

            public override int GetHashCode()
            {
                return base.GetHashCode();
            }
        }

        public class SubObjectSerializer : TablePropertySerializer
        {
            public override void Write(string name, object value, IDictionary<string, EntityProperty> properties, OperationContext operationContext)
            {
                SubObject val = value as SubObject;
                if (val != null)
                {
                    properties[name + ".A"] = EntityProperty.CreateEntityPropertyFromObject(val.A);
                    properties[name + ".B"] = EntityProperty.CreateEntityPropertyFromObject(val.B);
                }
            }

            public override object Read(Type targetType, string name, IDictionary<string, EntityProperty> properties, OperationContext operationContext)
            {
                EntityProperty aProp;
                EntityProperty bProp;
                if (properties.TryGetValue(name + ".A", out aProp) && properties.TryGetValue(name + ".B", out bProp))
                {
                    return new SubObject()
                    {
                        A = aProp.Int32Value.Value,
                        B = bProp.BooleanValue.Value
                    };
                }
                return null;
            }
        }

        public class MultiCaseSerializer : TablePropertySerializer
        {
            public override void Write(string name, object value, IDictionary<string, EntityProperty> properties, OperationContext operationContext)
            {
                string val = value as string;
                if (val != null)
                {
                    properties[name + ".Upper"] = EntityProperty.CreateEntityPropertyFromObject(val.ToUpper());
                    properties[name + ".Lower"] = EntityProperty.CreateEntityPropertyFromObject(val.ToLower());
                    properties[name] = EntityProperty.CreateEntityPropertyFromObject(val);
                }
            }

            public override object Read(Type targetType, string name, IDictionary<string, EntityProperty> properties, OperationContext operationContext)
            {
                EntityProperty prop;
                if (properties.TryGetValue(name, out prop))
                {
                    return prop.StringValue;
                }
                return null;
            }
        }

        public class IntEnumTypeConverter : TypeConverter
        {
            public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
            {
                return sourceType == typeof(string);
            }

            public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
            {
                return destinationType == typeof(string);
            }

            public override object ConvertFrom(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value)
            {
                string source = value as string;
                return Int32.Parse(source);
            }

            public override object ConvertTo(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value, Type destinationType)
            {
                if (destinationType == typeof(string))
                {
                    return ((int)value).ToString();
                }
                else
                {
                    return null;
                }
            }
        }
    }
}
