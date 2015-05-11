// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.WindowsAzure.Storage;
using NuGetGallery.Operations.Common;

namespace NuGetGallery.Operations
{
    internal static class TypeHelper
    {
        private static Dictionary<Type, Func<TypeConverter>> _additionalConverters = new Dictionary<Type, Func<TypeConverter>>() {
            { typeof(Stream), () => new FileStreamConverter() },
            {typeof(CloudStorageAccount), () => new CloudStorageAccountConverter()},
            {typeof(SqlConnectionStringBuilder), () => new SqlConnectionStringConverter()},
        };

        public static Type RemoveNullableFromType(Type type)
        {
            return Nullable.GetUnderlyingType(type) ?? type;
        }

        public static object ChangeType(object value, Type type)
        {
            if (type == null)
            {
                throw new ArgumentNullException("type");
            }

            if (value == null)
            {
                if (TypeAllowsNull(type))
                {
                    return null;
                }
                return Convert.ChangeType(value, type, CultureInfo.CurrentCulture);
            }

            type = RemoveNullableFromType(type);

            if (value.GetType() == type)
            {
                return value;
            }

            TypeConverter converter = TypeDescriptor.GetConverter(type);
            if (converter.CanConvertFrom(value.GetType()))
            {
                return converter.ConvertFrom(value);
            }

            converter = TypeDescriptor.GetConverter(value.GetType());
            if (converter.CanConvertTo(type))
            {
                return converter.ConvertTo(value, type);
            }

            Func<TypeConverter> ctor;
            if (_additionalConverters.TryGetValue(type, out ctor))
            {
                converter = ctor();
                if (converter.CanConvertFrom(value.GetType()))
                {
                    return converter.ConvertFrom(value);
                }
            }

            if (_additionalConverters.TryGetValue(value.GetType(), out ctor))
            {
                converter = ctor();
                if (converter.CanConvertTo(type))
                {
                    return converter.ConvertTo(value, type);
                }
            }

            throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture,
                TaskResources.UnableToConvertTypeError, value.GetType(), type));
        }

        public static bool TypeAllowsNull(Type type)
        {
            return Nullable.GetUnderlyingType(type) != null || !type.IsValueType;
        }

        public static Type GetGenericCollectionType(Type type)
        {
            return GetInterfaceType(type, typeof(ICollection<>));
        }

        public static Type GetDictionaryType(Type type)
        {
            return GetInterfaceType(type, typeof(IDictionary<,>));
        }

        private static Type GetInterfaceType(Type type, Type interfaceType)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == interfaceType)
            {
                return type;
            }
            return (from t in type.GetInterfaces()
                    where t.IsGenericType && t.GetGenericTypeDefinition() == interfaceType
                    select t).SingleOrDefault();
        }

        public static bool IsKeyValueProperty(PropertyInfo property)
        {
            return GetDictionaryType(property.PropertyType) != null;
        }

        public static bool IsMultiValuedProperty(PropertyInfo property)
        {
            return GetGenericCollectionType(property.PropertyType) != null || IsKeyValueProperty(property);
        }

        public static bool IsEnumProperty(PropertyInfo property)
        {
            return property.PropertyType.IsEnum;
        }
    }
}
