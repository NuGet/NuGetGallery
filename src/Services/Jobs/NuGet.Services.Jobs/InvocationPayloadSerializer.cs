using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace NuGet.Services.Jobs
{
    public static class InvocationPayloadSerializer
    {
        private static JsonSerializer _serializer = new JsonSerializer()
        {
            Formatting = Formatting.None
        };

        public static string Serialize(Dictionary<string, string> payload)
        {
            var builder = new StringBuilder();
            using (var writer = new StringWriter(builder))
            {
                _serializer.Serialize(writer, payload);
            }
            return builder.ToString();
        }

        public static Dictionary<string, string> Deserialize(string payload)
        {
            using (var reader = new JsonTextReader(new StringReader(payload)))
            {
                return _serializer.Deserialize<Dictionary<string, string>>(reader);
            }
        }
    }
}
