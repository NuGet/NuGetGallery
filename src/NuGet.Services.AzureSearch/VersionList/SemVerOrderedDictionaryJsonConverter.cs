// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using NuGet.Versioning;

namespace NuGet.Services.AzureSearch
{
    /// <summary>
    /// This is not strictly necessary but has proven useful for debugging the JSON blobs generated.
    /// </summary>
    public class SemVerOrderedDictionaryJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return typeof(IReadOnlyDictionary<string, VersionPropertiesData>).IsAssignableFrom(objectType);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            // Use default behavior for deserialization.
            return serializer.Deserialize<IReadOnlyDictionary<string, VersionPropertiesData>>(reader);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var dictionary = (IReadOnlyDictionary<string, VersionPropertiesData>)value;
            var pairs = dictionary
                .OrderBy(x =>
                {
                    NuGetVersion.TryParse(x.Key, out var version);
                    return version;
                })
                .ToList();

            writer.WriteStartObject();

            foreach (var pair in pairs)
            {
                writer.WritePropertyName(pair.Key);
                serializer.Serialize(writer, pair.Value);
            }

            writer.WriteEndObject();
        }
    }
}
