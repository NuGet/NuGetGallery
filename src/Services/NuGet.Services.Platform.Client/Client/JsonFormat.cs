using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Formatting;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace NuGet.Services.Client
{
    /// <summary>
    /// Defines the NuGet Services Json Format
    /// </summary>
    public static class JsonFormat
    {
        private static JsonSerializerSettings _serializerSettings;
        private static JsonMediaTypeFormatter _formatter;

        public static JsonSerializerSettings SerializerSettings { get { return _serializerSettings; } }
        public static JsonMediaTypeFormatter Formatter { get { return _formatter; } }

        static JsonFormat()
        {
            _serializerSettings = new JsonSerializerSettings()
            {
                ContractResolver = new NuGetContractResolver(),
                DateFormatHandling = DateFormatHandling.IsoDateFormat,
                DateParseHandling = DateParseHandling.DateTimeOffset,
                DateTimeZoneHandling = DateTimeZoneHandling.Utc,
                DefaultValueHandling = DefaultValueHandling.Include,
                Formatting = Formatting.Indented,
                MissingMemberHandling = MissingMemberHandling.Ignore,
                NullValueHandling = NullValueHandling.Ignore,
                ReferenceLoopHandling = ReferenceLoopHandling.Error,
                TypeNameHandling = TypeNameHandling.None
            };
            _serializerSettings.Converters.Add(new StringEnumConverter());
            _formatter = new JsonMediaTypeFormatter()
            {
                SerializerSettings = _serializerSettings
            };

            _formatter.SupportedMediaTypes.Clear();
            _formatter.SupportedMediaTypes.Add(new MediaTypeHeaderValue("application/json"));
        }

        public static T Deserialize<T>(string content)
        {
            return JsonConvert.DeserializeObject<T>(content, _serializerSettings);
        }

        public static string Serialize(object data)
        {
            return JsonConvert.SerializeObject(data, _serializerSettings);
        }

        public static Task<T> DeserializeAsync<T>(string content)
        {
            return JsonConvert.DeserializeObjectAsync<T>(content, _serializerSettings);
        }

        public static Task<string> SerializeAsync(object data)
        {
            return JsonConvert.SerializeObjectAsync(data, _serializerSettings.Formatting, _serializerSettings);
        }
    }

    public class NuGetContractResolver : CamelCasePropertyNamesContractResolver
    {
        protected override JsonDictionaryContract CreateDictionaryContract(Type objectType)
        {
            // Don't camel case dictionary keys
            JsonDictionaryContract contract = base.CreateDictionaryContract(objectType);
            contract.PropertyNameResolver = new Func<string, string>(s => s);
            return contract;
        }
    }
}
