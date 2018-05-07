// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NuGetGallery.Auditing.Obfuscation
{
    /// <summary>
    /// Implements a <see cref="JsonConverter"/> to be used for obfuscation.
    /// </summary>
    public class ObfuscatorJsonConverter : JsonConverter
    {
        private ObfuscationType _obfuscationType;

        /// <summary>
        /// The instance that will be serialized.
        /// </summary>
        /// <param name="instance"></param>
        public ObfuscatorJsonConverter(ObfuscationType obfuscationType)
        {
            _obfuscationType = obfuscationType;
        }

        public override bool CanConvert(Type objectType)
        {
            return true;
        }
        
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var t = JToken.FromObject(Obfuscate(value, _obfuscationType));
            t.WriteTo(writer);
        }

        public static string Obfuscate(object value, ObfuscationType obfuscationType)
        {
            switch (obfuscationType)
            {
                case ObfuscationType.Authors:
                    return string.Empty;
                case ObfuscationType.UserName:
                    return Obfuscator.ObfuscatedUserName;
                case ObfuscationType.UserKey:
                    return "-1";
                case ObfuscationType.IP:
                    return Obfuscator.ObfuscateIp(value.ToString());
                default:
                    throw new ArgumentException(nameof(obfuscationType));
            }
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override bool CanRead => false;
    }
}
