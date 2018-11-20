// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Newtonsoft.Json;

namespace NuGet.Protocol.Registration
{
    /// <summary>
    /// Source: https://docs.microsoft.com/en-us/nuget/api/registration-base-url-resource#registration-leaf
    /// </summary>
    public class RegistrationLeaf
    {
        [JsonProperty("@id")]
        public string Url { get; set; }

        [JsonProperty("catalogEntry")]
        public string CatalogEntry { get; set; }

        [JsonProperty("listed")]
        public bool? Listed { get; set; }

        [JsonProperty("packageContent")]
        public string PackageContent { get; set; }

        [JsonProperty("published")]
        public DateTimeOffset? Published { get; set;}

        [JsonProperty("registration")]
        public string Registration { get; set; }
    }
}
