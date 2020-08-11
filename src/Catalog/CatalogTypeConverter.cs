// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace NuGet.Services.Metadata.Catalog
{
    public class CatalogTypeConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(IEnumerable<string>);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            object type = null;
            if (reader.TokenType == JsonToken.StartArray)
            {
                // If the type is stored in an array, use the last value and then discard the rest.
                do
                {
                    reader.Read();
                    if (reader.TokenType == JsonToken.String)
                    {
                        type = reader.Value;
                    }
                } while (reader.TokenType != JsonToken.EndArray);
            }
            else
            {
                type = reader.Value;
            }

            if (type == null)
            {
                throw new InvalidDataException("Failed to parse the type of a catalog entry!");
            }

            return new string[] { type.ToString() };
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var types = value as IEnumerable<string>;
            serializer.Serialize(writer, types.First());
        }
    }
}