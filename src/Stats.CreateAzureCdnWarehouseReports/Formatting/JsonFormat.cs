// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net.Http.Formatting;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Stats.CreateAzureCdnWarehouseReports
{
    /// <summary>
    /// Defines the NuGet Services Json Format
    /// </summary>
    public static class JsonFormat
    {
        private static readonly JsonSerializerSettings _serializerSettings;
        private static readonly JsonMediaTypeFormatter _formatter;

        static JsonFormat()
        {
            _serializerSettings = new JsonSerializerSettings
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

            _formatter = new JsonMediaTypeFormatter
            {
                SerializerSettings = _serializerSettings
            };

            _formatter.SupportedMediaTypes.Clear();
            _formatter.SupportedMediaTypes.Add(new MediaTypeHeaderValue("application/json"));
        }

        public static JsonSerializerSettings SerializerSettings { get { return _serializerSettings; } }
        public static JsonMediaTypeFormatter Formatter { get { return _formatter; } }

        public static T Deserialize<T>(string content)
        {
            return JsonConvert.DeserializeObject<T>(content, _serializerSettings);
        }

        public static string Serialize(object data)
        {
            return JsonConvert.SerializeObject(data, _serializerSettings);
        }
    }
}