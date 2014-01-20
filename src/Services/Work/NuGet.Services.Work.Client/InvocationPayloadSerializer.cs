using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace NuGet.Services.Work
{
    public static class InvocationPayloadSerializer
    {
        private static JsonSerializer _serializer = new JsonSerializer()
        {
            Formatting = Formatting.None
        };

        public static string Serialize(Dictionary<string, string> payload)
        {
            if (payload == null)
            {
                return null;
            }

            var builder = new StringBuilder();
            using (var writer = new StringWriter(builder))
            {
                _serializer.Serialize(writer, payload);
            }
            return builder.ToString();
        }

        public static Dictionary<string, string> Deserialize(string payload)
        {
            if (String.IsNullOrEmpty(payload))
            {
                return null;
            }

            using (var reader = new JsonTextReader(new StringReader(payload)))
            {
                var original = _serializer.Deserialize<Dictionary<string, string>>(reader);
                
                // Rebuild the dictionary with a OrdinalIgnoreCase key comparer
                return new Dictionary<string, string>(original, StringComparer.OrdinalIgnoreCase);
            }
        }
    }
}
