// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using Newtonsoft.Json;

namespace NuGet.Protocol.Catalog.Serialization
{
    public class PackageDependencyRangeConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(string);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.StartArray)
            {
                // There are some quirky packages with arrays of dependency version ranges. In this case, we take the
                // first element.
                // Example: https://api.nuget.org/v3/catalog0/data/2016.02.21.11.06.01/dingu.generic.repo.ef7.1.0.0-beta2.json
                var array = serializer.Deserialize<string[]>(reader);
                return array.FirstOrDefault();
            }

            return serializer.Deserialize<string>(reader);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, value);
        }
    }
}
