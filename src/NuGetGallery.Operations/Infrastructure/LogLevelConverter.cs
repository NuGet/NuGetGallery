using System;
using System.Collections.Generic;
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
                reader.Read();
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
                        return LogLevel.FromString((string)reader.Value);
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
