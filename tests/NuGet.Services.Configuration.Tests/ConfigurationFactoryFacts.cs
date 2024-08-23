// Copyright (c) .NET Foundation. All rights reserved. 
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information. 

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using Xunit;

namespace NuGet.Services.Configuration.Tests
{
    public class ConfigurationFactoryFacts
    {
        /// <summary>
        /// Represents a subclass of <see cref="Configuration"/>.
        /// Used to construct a class dynamically to test the <see cref="ConfigurationFactory"/> with.
        /// </summary>
        public class ConfigurationClassInfo
        {
            public ConfigurationClassInfo(IDictionary<string, ConfigurationPropertyInfo> propertyMap, string classPrefix)
                : this(propertyMap)
            {
                ClassPrefix = classPrefix;
            }

            public ConfigurationClassInfo(IDictionary<string, ConfigurationPropertyInfo> propertyMap)
            {
                PropertyMap = propertyMap;
            }

            /// <summary>
            /// Describes each property of the object.
            /// Each key represents the name of a property.
            /// Each <see cref="ConfigurationPropertyInfo"/> describes additional information about the property.
            /// </summary>
            public IDictionary<string, ConfigurationPropertyInfo> PropertyMap { get; set; }

            /// <summary>
            /// If not null, a <see cref="ConfigurationKeyPrefixAttribute"/> will be added to the class with this value as the prefix.
            /// </summary>
            public string ClassPrefix { get; set; }
        }

        /// <summary>
        /// Represents the data associated with a property of a subclass of <see cref="Configuration"/>.
        /// Used to construct a class dynamically to test the <see cref="ConfigurationFactory"/> with.
        /// </summary>
        public class ConfigurationPropertyInfo
        {
            public ConfigurationPropertyInfo(Type type, bool required, object expectedValue, object defaultValue = null,
                string configKey = null, string configKeyPrefix = null)
            {
                Type = type;
                Required = required;
                ExpectedValue = expectedValue;
                DefaultValue = defaultValue;
                ConfigKey = configKey;
                ConfigKeyPrefix = configKeyPrefix;
            }

            /// <summary>
            /// If not null, a <see cref="ConfigurationKeyAttribute"/> will be added to the property with this value as the key.
            /// </summary>
            public string ConfigKey { get; set; }

            /// <summary>
            /// If not null, a <see cref="ConfigurationKeyPrefixAttribute"/> will be added to the property with this value as the prefix.
            /// </summary>
            public string ConfigKeyPrefix { get; set; }

            /// <summary>
            /// The type of the property.
            /// </summary>
            public Type Type { get; set; }

            /// <summary>
            /// If true, a <see cref="RequiredAttribute"/> will be added to the property.
            /// </summary>
            public bool Required { get; set; }

            /// <summary>
            /// If not null, the value to be injected into the property by the <see cref="ConfigurationFactory"/>.
            /// If null, the default value will be injected into the property by the <see cref="ConfigurationFactory"/>.
            /// </summary>
            public object ExpectedValue { get; set; }

            /// <summary>
            /// If not null, a <see cref="DefaultValueAttribute"/> will be added to this property with this value as the default.
            /// If null, <code>default(Type)</code> will be used as the default.
            /// </summary>
            public object DefaultValue { get; set; }
        }

        /// <summary>
        /// Dummy class that has no conversion into any type.
        /// Used to test that the <see cref="ConfigurationFactory"/> fails correctly when given incorrect types.
        /// </summary>
        public class NoConversionFromThisClass
        {
        }

        /// <summary>
        /// Test data for the <see cref="ConfigurationFactory"/> tests.
        /// </summary>
        public static IEnumerable<object[]> ConfigurationFactoryTestData => new[]
        {
            new object[]
            {
                // Succeeds
                // Tests base case
                new ConfigurationClassInfo(new Dictionary<string, ConfigurationPropertyInfo>
                {
                    {
                        "stringProperty",
                        new ConfigurationPropertyInfo(typeof(string), required: false, expectedValue: "i am a string")
                    }
                })
            },
            new object[]
            {
                // Succeeds
                // Tests ConfigurationKeyAttribute
                new ConfigurationClassInfo(new Dictionary<string, ConfigurationPropertyInfo>
                {
                    {
                        "stringPropertyWithCustomKey",
                        new ConfigurationPropertyInfo(typeof(string), required: false,
                            expectedValue: "i have a custom name!",
                            configKey: "customConfig")
                    }
                })
            },
            new object[]
            {
                // Succeeds
                // Tests ConfigurationKeyPrefixAttribute
                new ConfigurationClassInfo(new Dictionary<string, ConfigurationPropertyInfo>
                {
                    {
                        "stringPropertyWithPrefix",
                        new ConfigurationPropertyInfo(typeof(string), required: false,
                            expectedValue: "i have a cool prefix!",
                            configKeyPrefix: "coolbeans:")
                    }
                })
            },
            new object[]
            {
                // Succeeds
                // Tests ConfigurationKeyAttribute and ConfigurationKeyPrefixAttribute together
                new ConfigurationClassInfo(new Dictionary<string, ConfigurationPropertyInfo>
                {
                    {
                        "stringPropertyWithPrefixAndCustomKey",
                        new ConfigurationPropertyInfo(typeof(string), required: false,
                            expectedValue: "i have a cool prefix and a cool name!", configKey: "customConfig",
                            configKeyPrefix: "coolbeans:")
                    }
                })
            },
            new object[]
            {
                // Succeeds
                // Tests ConfigurationKeyPrefixAttribute on class
                new ConfigurationClassInfo(new Dictionary<string, ConfigurationPropertyInfo>
                {
                    {
                        "stringPropertyWithInheritedPrefix",
                        new ConfigurationPropertyInfo(typeof(string), required: false,
                            expectedValue: "i inherit my prefix!")
                    }
                }, "coolbeans:")
            },
            new object[]
            {
                // Succeeds
                // Tests required
                new ConfigurationClassInfo(new Dictionary<string, ConfigurationPropertyInfo>
                {
                    {
                        "stringPropertyRequired",
                        new ConfigurationPropertyInfo(typeof(string), required: true,
                            expectedValue: "i am still a string")
                    }
                })
            },
            new object[]
            {
                // Succeeds
                // Tests default value
                new ConfigurationClassInfo(new Dictionary<string, ConfigurationPropertyInfo>
                {
                    {
                        "stringPropertyWithDefault",
                        new ConfigurationPropertyInfo(typeof(string), required: false, expectedValue: null,
                            defaultValue: "default string value")
                    }
                })
            },
            new object[]
            {
                // Succeeds
                // Tests default value with actual value provided
                new ConfigurationClassInfo(new Dictionary<string, ConfigurationPropertyInfo>
                {
                    {
                        "stringPropertyWithDefaultDefined",
                        new ConfigurationPropertyInfo(typeof(string), required: false,
                            expectedValue: "yet again i am a string",
                            defaultValue: "default string value")
                    }
                })
            },
            new object[]
            {
                // Succeeds
                // Tests required and default together
                new ConfigurationClassInfo(new Dictionary<string, ConfigurationPropertyInfo>
                {
                    {
                        "stringPropertyRequiredWithDefaultDefined",
                        new ConfigurationPropertyInfo(typeof(string), required: true, expectedValue: "string forever",
                            defaultValue: "default string value")
                    }
                })
            },
            new object[]
            {
                // Fails because required configuration is missing
                // Tests required
                new ConfigurationClassInfo(new Dictionary<string, ConfigurationPropertyInfo>
                {
                    {
                        "stringPropertyRequiredMissing",
                        new ConfigurationPropertyInfo(typeof(string), required: true, expectedValue: null)
                    }
                })
            },
            new object[]
            {
                // Succeeds because missing but not required
                // Tests default without provided default
                new ConfigurationClassInfo(new Dictionary<string, ConfigurationPropertyInfo>
                {
                    {
                        "stringPropertyMissing",
                        new ConfigurationPropertyInfo(typeof(string), required: false, expectedValue: null)
                    }
                })
            },
            new object[]
            {
                // Fails because empty string is equivalent to null with regards to configuration
                // (the IConfigurationProvider throws ArgumentException)
                new ConfigurationClassInfo(new Dictionary<string, ConfigurationPropertyInfo>
                {
                    {
                        "stringPropertyRequiredEmpty",
                        new ConfigurationPropertyInfo(typeof(string), required: true, expectedValue: "")
                    }
                })
            },
            new object[]
            {
                // Succeeds
                // Tests conversion into types other than string
                new ConfigurationClassInfo(new Dictionary<string, ConfigurationPropertyInfo>
                {
                    {"intProperty", new ConfigurationPropertyInfo(typeof(int), required: false, expectedValue: 101)}
                })
            },
            new object[]
            {
                // Succeeds
                // Tests multiple properties in a class
                new ConfigurationClassInfo(new Dictionary<string, ConfigurationPropertyInfo>
                {
                    {
                        "stringProperty1",
                        new ConfigurationPropertyInfo(typeof(string), required: false, expectedValue: "thing 1")
                    },
                    {
                        "stringProperty2",
                        new ConfigurationPropertyInfo(typeof(string), required: false, expectedValue: "thing 2")
                    }
                })
            },
            new object[]
            {
                // Succeeds
                // Tests ConfigurationKeyPrefixAttribute on class with multiple properties
                new ConfigurationClassInfo(new Dictionary<string, ConfigurationPropertyInfo>
                {
                    {
                        "stringPropertyWithInheritedPrefix1",
                        new ConfigurationPropertyInfo(typeof(string), required: false, expectedValue: "fashionable felines")
                    },
                    {
                        "stringPropertyWithInheritedPrefix2",
                        new ConfigurationPropertyInfo(typeof(string), required: false, expectedValue: "fish of various colors")
                    }
                }, "Seuss:")
            },
            new object[]
            {
                // Succeeds
                // Tests ConfigurationKeyPrefixAttribute on class with multiple properties and override
                new ConfigurationClassInfo(new Dictionary<string, ConfigurationPropertyInfo>
                {
                    {
                        "stringPropertyWithInheritedPrefix",
                        new ConfigurationPropertyInfo(typeof(string), required: false, expectedValue: "small mustached yellow environmentalist")
                    },
                    {
                        "stringPropertyWithOverride",
                        new ConfigurationPropertyInfo(typeof(string), required: false, expectedValue: "gandalf", configKeyPrefix: "LOTR:")
                    }
                }, "Seuss:")
            },
            new object[]
            {
                // Succeeds
                // Tests multiple properties and types in a class
                new ConfigurationClassInfo(new Dictionary<string, ConfigurationPropertyInfo>
                {
                    {"intProperty", new ConfigurationPropertyInfo(typeof(int), required: false, expectedValue: 44)},
                    {
                        "requiredBoolProperty",
                        new ConfigurationPropertyInfo(typeof(bool), required: true, expectedValue: true)
                    }
                })
            },
            new object[]
            {
                // Succeeds
                // Tests multiple properties and types in a class and custom key name
                new ConfigurationClassInfo(new Dictionary<string, ConfigurationPropertyInfo>
                {
                    {
                        "doublePropertyWithCustomKey",
                        new ConfigurationPropertyInfo(typeof(double), required: false, expectedValue: 99.999,
                            configKey: "coolIntProperty")
                    },
                    {
                        "requiredDatetimePropertyWithPrefix",
                        new ConfigurationPropertyInfo(typeof(bool), required: true, expectedValue: DateTime.MinValue,
                            configKeyPrefix: "coolProperty.")
                    },
                    {
                        "intPropertyWithDefaultAndCustomKeyAndPrefix",
                        new ConfigurationPropertyInfo(typeof(int), required: false, expectedValue: 503,
                            defaultValue: 200,
                            configKey: "ResponseCode", configKeyPrefix: "coolProperty:")
                    }
                })
            },
            new object[]
            {
                // Succeeds
                // Tests defaults with multiple properties and types in a class
                new ConfigurationClassInfo(new Dictionary<string, ConfigurationPropertyInfo>
                {
                    {
                        "doublePropertyWithDefault",
                        new ConfigurationPropertyInfo(typeof(double), required: false, expectedValue: 1.1,
                            defaultValue: 3.14)
                    },
                    {
                        "boolPropertyMissingWithDefault",
                        new ConfigurationPropertyInfo(typeof(bool), required: false, expectedValue: null,
                            defaultValue: true)
                    }
                })
            },
            new object[]
            {
                // Fails
                // Tests that the configuration provided must have the same type as the property
                new ConfigurationClassInfo(new Dictionary<string, ConfigurationPropertyInfo>
                {
                    {
                        "invalidBoolProperty",
                        new ConfigurationPropertyInfo(typeof(double), required: false,
                            expectedValue: new NoConversionFromThisClass())
                    }
                })
            },
            new object[]
            {
                // Fails
                // Tests that the default provided must be convertible to the property
                new ConfigurationClassInfo(new Dictionary<string, ConfigurationPropertyInfo>
                {
                    {
                        "boolPropertyWithInvalidDefault",
                        new ConfigurationPropertyInfo(typeof(double), required: false, expectedValue: null,
                            defaultValue: "can't convert this to double")
                    }
                })
            },
            new object[]
            {
                // Succeeds
                // Tests that the class supports providing null data to a nullable property
                new ConfigurationClassInfo(new Dictionary<string, ConfigurationPropertyInfo>
                {
                    {
                        "nullableIntPropertyAsNull",
                        new ConfigurationPropertyInfo(typeof(int?), required: false, expectedValue: null)
                    }
                })
            },
            new object[]
            {
                // Succeeds
                // Tests that the class supports providing non-null data to a nullable property
                new ConfigurationClassInfo(new Dictionary<string, ConfigurationPropertyInfo>
                {
                    {
                        "nullableIntPropertyAsInt",
                        new ConfigurationPropertyInfo(typeof(int?), required: false, expectedValue: 2439)
                    }
                })
            },
            new object[]
            {
                // Fails because the expected value cannot be converted from a string to an int
                // Tests that the configuration provided for a nullable must have the same type as the property
                new ConfigurationClassInfo(new Dictionary<string, ConfigurationPropertyInfo>
                {
                    {
                        "nullableIntPropertyAsString",
                        new ConfigurationPropertyInfo(typeof(int?), required: false, expectedValue: "this isn't right!")
                    }
                })
            }
        };

        /// <summary>
        /// Returns true if the object is convertible to type.
        /// </summary>
        /// <param name="value">The object to test.</param>
        /// <param name="type">Type to test.</param>
        /// <returns>True if the <param name="value">object</param> is convertible to <param name="type">type</param>, false otherwise.</returns>
        private static bool IsValueValid(object value, Type type)
        {
            if (value == null)
            {
                // Null is always valid because it will use the default.
                return true;
            }

            if (value.GetType() == type)
            {
                // TypeConverters sometimes throw when converting a type to the same type.
                return true;
            }

            // Attempt to convert the value.
            // If it fails, the value is not valid.
            try
            {
                TypeDescriptor.GetConverter(type).ConvertFrom(value);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Tests that the <see cref="ConfigurationFactory"/> can handle a subclass of <see cref="Configuration"/> described by <param name="configClass">configClass</param>.
        /// </summary>
        /// <param name="configClass">Describes the subclass of <see cref="Configuration"/> to dynamically build and test.</param>
        [Theory]
        [MemberData(nameof(ConfigurationFactoryTestData))]
        public void CorrectlyHandlesTypes(ConfigurationClassInfo configClass)
        {
            // Arrange

            IConfigurationFactory configFactory =
                new ConfigurationFactory(
                    new DictionaryConfigurationProvider(
                        configClass.PropertyMap.ToDictionary(
                            property => (property.Value.ConfigKeyPrefix ?? configClass.ClassPrefix) + (property.Value.ConfigKey ?? property.Key),
                            property => property.Value.ExpectedValue)));

            var type = CreateTypeFromConfiguration(configClass);

            // Act
            var getConfig = new Func<object>(() => GetType()
                .GetMethod(nameof(GetConfig))
                .MakeGenericMethod(type)
                .Invoke(this, new object[] {configFactory}));

            var willSucceed = true;
            foreach (var configPair in configClass.PropertyMap)
            {
                var configPropertyInfo = configPair.Value;

                // True if the expected value is null or if it is an empty string.
                var expectedValueIsMissing =
                    configPropertyInfo.ExpectedValue == null ||
                    (configPropertyInfo.ExpectedValue is string && string.IsNullOrEmpty((string) configPropertyInfo.ExpectedValue));

                var isExpectedValueValid = IsValueValid(configPropertyInfo.ExpectedValue, configPropertyInfo.Type);
                var isDefaultValueValid = IsValueValid(configPropertyInfo.DefaultValue, configPropertyInfo.Type);

                if ((configPropertyInfo.Required && expectedValueIsMissing) ||
                    !isExpectedValueValid ||
                    !isDefaultValueValid ||
                    configPropertyInfo.ConfigKey == string.Empty)
                {
                    // Acquiring the configuration will fail if a required attribute does not have an expected value.
                    // It will also fail if the expected value or default value cannot be converted into the type of the property.
                    // A null or empty configuration key will also fail.
                    willSucceed = false;
                    break;
                }
            }

            // Assert
            if (willSucceed)
            {
                var config = getConfig();
                foreach (var configPropertyPair in configClass.PropertyMap)
                {
                    var configPropertyInfo = configPropertyPair.Value;
                    Assert.Equal(configPropertyInfo.ExpectedValue ?? configPropertyInfo.DefaultValue,
                        GetFromProperty(config, configPropertyPair.Key));
                }
            }
            else
            {
                // This will throw a TargetInvocationException instead of the exception thrown by ConfigurationFactory because we are using Reflection in getConfig.
                Assert.Throws<TargetInvocationException>(getConfig);
            }
        }

        public T GetConfig<T>(IConfigurationFactory configFactory) where T : Configuration, new()
        {
            return configFactory.Get<T>().Result;
        }

        private static object GetFromProperty(object instance, string propertyName)
        {
            return instance.GetType().GetProperty(propertyName).GetMethod.Invoke(instance, null);
        }

        private static Type CreateTypeFromConfiguration(ConfigurationClassInfo configClass)
        {
            var typeBuilder = CreateTypeBuilder();

            if (!string.IsNullOrEmpty(configClass.ClassPrefix))
            {
                typeBuilder.SetCustomAttribute(new CustomAttributeBuilder(
                    typeof(ConfigurationKeyPrefixAttribute).GetConstructor(new[] {typeof(string)}),
                    new object[] {configClass.ClassPrefix}));
            }

            foreach (var property in configClass.PropertyMap)
            {
                AddProperty(typeBuilder, property.Key, property.Value);
            }

            return typeBuilder.CreateType();
        }

        private const string DynamicTypeName = "TestConfiguration";
        private static int DynamicTypeCount = 0;

        private static TypeBuilder CreateTypeBuilder()
        {
            var assemblyName = new AssemblyName(Assembly.GetExecutingAssembly().FullName);
            var assemblyBuilder = Thread.GetDomain().DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
            var moduleBuilder = assemblyBuilder.DefineDynamicModule(typeof(ConfigurationFactoryFacts).Module.FullyQualifiedName);
            return moduleBuilder.DefineType($"{DynamicTypeName}{DynamicTypeCount++}", TypeAttributes.Public | TypeAttributes.Class,
                typeof(Configuration));
        }

        /// <summary>
        /// Dynamically adds a property to a <see cref="TypeBuilder"/>.
        /// </summary>
        /// <param name="typeBuilder">The <see cref="TypeBuilder"/> to add the property to.</param>
        /// <param name="name">The name of the property.</param>
        /// <param name="configPropertyInfo">Specifies how to construct the property.</param>
        private static void AddProperty(TypeBuilder typeBuilder, string name, ConfigurationPropertyInfo configPropertyInfo)
        {
            // Create the property attribute.
            var propertyAttributes = configPropertyInfo.DefaultValue != null ? PropertyAttributes.HasDefault : PropertyAttributes.None;
            var propertyBuilder = typeBuilder.DefineProperty(name, propertyAttributes, configPropertyInfo.Type, parameterTypes: null);

            if (configPropertyInfo.Required)
            {
                propertyBuilder.SetCustomAttribute(
                    new CustomAttributeBuilder(typeof(RequiredAttribute).GetConstructor(Type.EmptyTypes),
                        new object[] { }));
            }

            if (configPropertyInfo.DefaultValue != null)
            {
                propertyBuilder.SetCustomAttribute(
                    new CustomAttributeBuilder(typeof(DefaultValueAttribute).GetConstructor(new[] { configPropertyInfo.DefaultValue.GetType() }),
                        new[] { configPropertyInfo.DefaultValue }));
            }

            if (configPropertyInfo.ConfigKey != null)
            {
                propertyBuilder.SetCustomAttribute(
                    new CustomAttributeBuilder(
                        typeof(ConfigurationKeyAttribute).GetConstructor(new[] { typeof(string) }),
                        new object[] { configPropertyInfo.ConfigKey }));
            }

            if (configPropertyInfo.ConfigKeyPrefix != null)
            {
                propertyBuilder.SetCustomAttribute(
                    new CustomAttributeBuilder(
                        typeof(ConfigurationKeyPrefixAttribute).GetConstructor(new[] { typeof(string) }),
                        new object[] { configPropertyInfo.ConfigKeyPrefix }));
            }

            // Create the field that the property will get and set.
            var fieldBuilder = typeBuilder.DefineField($"_{name}", configPropertyInfo.Type, FieldAttributes.Private);

            // Create the get method for the property that will return the field.
            var getMethod = typeBuilder.DefineMethod($"get_{name}",
                MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName, configPropertyInfo.Type, parameterTypes: null);

            var getMethodIl = getMethod.GetILGenerator();
            getMethodIl.Emit(OpCodes.Ldarg_0);
            getMethodIl.Emit(OpCodes.Ldfld, fieldBuilder);
            getMethodIl.Emit(OpCodes.Ret);

            propertyBuilder.SetGetMethod(getMethod);

            // Create the set method for the property that will set the field.
            var setMethod = typeBuilder.DefineMethod($"set_{name}",
                MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName, typeof(void),
                new[] { configPropertyInfo.Type });

            var setMethodIl = setMethod.GetILGenerator();
            setMethodIl.Emit(OpCodes.Ldarg_0);
            setMethodIl.Emit(OpCodes.Ldarg_1);
            setMethodIl.Emit(OpCodes.Stfld, fieldBuilder);
            setMethodIl.Emit(OpCodes.Ret);

            propertyBuilder.SetSetMethod(setMethod);
        }
    }
}
