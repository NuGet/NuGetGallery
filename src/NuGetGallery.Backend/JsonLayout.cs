// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using NLog.Layouts;

namespace NuGetGallery.Backend
{
    public class JsonLayout : Layout
    {
        private static readonly JsonSerializerSettings _defaultSettings = new JsonSerializerSettings()
        {
            Formatting = Formatting.None,
            TypeNameHandling = TypeNameHandling.Auto,
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            DateFormatHandling = DateFormatHandling.IsoDateFormat,
            NullValueHandling = NullValueHandling.Include,
            ReferenceLoopHandling = ReferenceLoopHandling.Serialize,
        };

        public JsonSerializerSettings SerializerSettings { get; private set; }

        public JsonLayout() : this(_defaultSettings) { }
        public JsonLayout(JsonSerializerSettings serializerSettings)
        {
            SerializerSettings = serializerSettings;
        }

        protected override string GetFormattedMessage(NLog.LogEventInfo logEvent)
        {
            var obj = new
            {
                Timestamp = logEvent.TimeStamp.ToUniversalTime(),
                Message = logEvent.FormattedMessage,
                Level = logEvent.Level.Name,
                Exception = logEvent.Exception,
                Logger = logEvent.LoggerName,
                FullEvent = logEvent
            };

            return JsonConvert.SerializeObject(obj, Formatting.None, SerializerSettings);
        }
    }
}
