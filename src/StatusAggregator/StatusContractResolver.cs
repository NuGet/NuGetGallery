// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace StatusAggregator
{
    /// <summary>
    /// Implementation of <see cref="DefaultContractResolver"/> used by <see cref="StatusExporter"/> such that empty fields and arrays are not serialized.
    /// </summary>
    public class StatusContractResolver : DefaultContractResolver
    {
        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            JsonProperty property = base.CreateProperty(member, memberSerialization);

            var propertyType = property.PropertyType;

            if (propertyType == typeof(string))
            {
                // Do not serialize strings if they are null or empty.
                property.ShouldSerialize = instance => !string.IsNullOrEmpty((string)instance);
            }

            if (typeof(IEnumerable).IsAssignableFrom(propertyType))
            {
                SetShouldSerializeForIEnumerable(property, member);
            }

            return property;
        }

        private void SetShouldSerializeForIEnumerable(JsonProperty property, MemberInfo member)
        {
            Func<object, object> getValue;

            // Create a function to get the value of the member using its type.
            switch (member.MemberType)
            {
                case MemberTypes.Field:
                    getValue = instance => ((FieldInfo)member).GetValue(instance);
                    break;
                case MemberTypes.Property:
                    getValue = instance => ((PropertyInfo)member).GetValue(instance);
                    break;
                default:
                    return;
            }

            // Do not serialize an IEnumerable if it is null or empty
            property.ShouldSerialize = instance =>
            {
                var value = (IEnumerable)getValue(instance);

                if (value == null)
                {
                    return false;
                }

                foreach (var obj in value)
                {
                    return true;
                }

                return false;
            };
        }
    }
}
