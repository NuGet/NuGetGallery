using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;

namespace NuGet.Services
{
    internal class AssemblyFullNameConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(AssemblyName);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.String)
            {
                return new AssemblyName(reader.ReadAsString());
            }
            return null;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            AssemblyName asmName = value as AssemblyName;
            if (asmName != null)
            {
                writer.WriteValue(asmName.FullName);
            }
        }
    }
}
