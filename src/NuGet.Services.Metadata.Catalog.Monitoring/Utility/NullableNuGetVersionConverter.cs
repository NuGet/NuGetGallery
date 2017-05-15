// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Protocol;
using NuGet.Versioning;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    /// <summary>
    /// <see cref="NuGetVersionConverter"/> that accepts null <see cref="NuGetVersion"/>s.
    /// Calls into existing <see cref="NuGetVersionConverter"/> when non-null.
    /// </summary>
    public class NullableNuGetVersionConverter : NuGetVersionConverter
    {
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null || reader.Value == null)
            {
                return null;
            }

            if (reader.TokenType == JsonToken.String && 
                string.IsNullOrEmpty(new JValue(reader.Value).ToString()))
            {
                return null;
            }

            return base.ReadJson(reader, objectType, existingValue, serializer);
        }
    }
}
