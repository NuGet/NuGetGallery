// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Newtonsoft.Json;

namespace NuGet.Protocol.Catalog
{
    /// <summary>
    /// Source: https://docs.microsoft.com/en-us/nuget/api/registration-base-url-resource#package-deprecation
    /// </summary>
    public class PackageDeprecation
    {
        [JsonProperty("@id")]
        public string Url { get; set; }

        [JsonProperty("@type")]
        public string Type { get; set; }

        [JsonProperty("alternatePackage")]
        public AlternatePackage AlternatePackage { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("reasons")]
        public List<string> Reasons { get; set; }
    }
}
