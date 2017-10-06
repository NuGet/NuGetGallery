// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using NuGet.Protocol;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    public static class JsonSerializerUtility
    {
        /// <summary>
        /// The <see cref="JsonSerializerSettings"/> to use.
        /// </summary>
        public static JsonSerializerSettings SerializerSettings
        {
            get
            {
                var settings = new JsonSerializerSettings();

                settings.Converters.Add(new NuGetVersionConverter());
                settings.Converters.Add(new StringEnumConverter());

                return settings;
            }
        }
    }
}