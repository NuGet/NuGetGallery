// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Newtonsoft.Json;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    /// <summary>
    /// <see cref="JsonConverter"/> for converting exceptions safely.
    /// If the exception fails to deserialize, returns an <see cref="ExceptionDeserializationException"/> instead of failing.
    /// </summary>
    public class SafeExceptionConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return typeof(Exception).IsAssignableFrom(objectType);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            try
            {
                return serializer.Deserialize(reader, objectType);
            }
            catch (Exception e)
            {
                // When deserializing the exception fails, we don't want to fail deserialization of the entire object.
                // Return the exception that was thrown instead.
                return e;
            }
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, value);
        }
    }
}