// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NuGetGallery.Auditing.Obfuscation
{
    /// <summary>
    /// Implements a <see cref="JsonConverter"/> to be used for obfuscation.
    /// </summary>
    public class ObfuscatorJsonConverter : JsonConverter
    {
        private object Instance { get; }

        /// <summary>
        /// The instance that will be serialized.
        /// </summary>
        /// <param name="instance"></param>
        public ObfuscatorJsonConverter(object instance)
        {
            Instance = instance ?? throw new ArgumentNullException(nameof(instance));
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(string) ||
                   objectType == typeof(int?);
        }

        /// <summary>
        /// Gets the <see cref="PropertyInfo"/> from a full property path.
        /// </summary>
        /// <param name="path"></param>
        /// <returns>The <see cref="PropertyInfo"/> from the <paramref name="path"/>.</returns>
        protected virtual PropertyInfo ResolvePath(string path)
        {
            PropertyInfo property = null;
            var currentInstance = Instance;
            foreach (var part in path.Split('.'))
            {
                property = currentInstance.GetType().GetProperty(part);
                if (property == null) { return null; }
                currentInstance = property.GetValue(currentInstance);
            }
            return property;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var property = ResolvePath(writer.Path);
            if (property != null)
            {
                var obfuscatorAttribute = property.GetCustomAttribute(typeof(ObfuscateAttribute));

                if (obfuscatorAttribute != null)
                {
                    var obfuscationType = ((ObfuscateAttribute)obfuscatorAttribute).ObfuscationType;
                    value = Obfuscate(value, obfuscationType);
                }
            }
            var t = JToken.FromObject(value);
            t.WriteTo(writer);
        }

        /// <summary>
        /// Obfuscates values based on the <see cref="ObfuscationType"/>.
        /// </summary>
        /// <param name="value">The value to be obfuscated.</param>
        /// <param name="obfuscationType">The type of obfuscation.</param>
        /// <returns>The obfuscated value.</returns>
        private string Obfuscate(object value, ObfuscationType obfuscationType)
        {
            switch(obfuscationType)
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
