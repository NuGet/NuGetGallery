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

namespace Stats.CreateWarehouseReports
{
    /// <summary>
    /// Defines the NuGet Services Json Format
    /// </summary>
    public static class JsonFormat
    {
        private static JsonSerializerSettings _serializerSettings;
        private static JsonSerializerSettings _nonCamelCasedSettings;
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

            _nonCamelCasedSettings = new JsonSerializerSettings()
            {
                // ContractResolver = ...
                DateFormatHandling = _serializerSettings.DateFormatHandling,
                DateParseHandling = _serializerSettings.DateParseHandling,
                DateTimeZoneHandling = _serializerSettings.DateTimeZoneHandling,
                DefaultValueHandling = _serializerSettings.DefaultValueHandling,
                Formatting = _serializerSettings.Formatting,
                MissingMemberHandling = _serializerSettings.MissingMemberHandling,
                NullValueHandling = _serializerSettings.NullValueHandling,
                ReferenceLoopHandling = _serializerSettings.ReferenceLoopHandling,
                TypeNameHandling = _serializerSettings.TypeNameHandling
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

        public static string Serialize(object data) { return Serialize(data, camelCase: true); }
        public static string Serialize(object data, bool camelCase)
        {
            var settings = camelCase ? _serializerSettings : _nonCamelCasedSettings;
            return JsonConvert.SerializeObject(data, _serializerSettings);
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

        protected override JsonObjectContract CreateObjectContract(Type objectType)
        {
            return base.CreateObjectContract(objectType);
        }
    }
}