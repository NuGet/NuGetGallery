// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using NLog;

namespace NuGetGallery.Operations.Infrastructure
{
    public class LogLevelConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(LogLevel);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.String)
            {
                var ret = LogLevel.FromString((string)reader.Value);
                return ret;
            }
            else if (reader.TokenType == JsonToken.StartObject)
            {
                reader.Read();
                if (reader.TokenType == JsonToken.PropertyName && String.Equals((string)reader.Value, "name", StringComparison.OrdinalIgnoreCase))
                {
                    reader.Read();
                    if (reader.TokenType == JsonToken.String)
                    {
                        string val = (string)reader.Value;
                        reader.Read();
                        Debug.Assert(reader.TokenType == JsonToken.EndObject);
                        return LogLevel.FromString(val);
                    }
                }
            }
            return null;

        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            writer.WriteValue(((LogLevel)value).Name);
        }
    }
}
