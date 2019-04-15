// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;

namespace NuGet.Protocol.Registration
{
    /// <summary>
    /// Source: https://docs.microsoft.com/en-us/nuget/api/registration-base-url-resource#catalog-entry
    /// </summary>
    public class RegistrationCatalogEntry
    {
        [JsonProperty("@id")]
        public string Url { get; set; }

        [JsonProperty("version")]
        public string Version { get; set; }

        [JsonProperty("listed")]
        [JsonRequired]
        public bool Listed { get; set; }
    }
}
