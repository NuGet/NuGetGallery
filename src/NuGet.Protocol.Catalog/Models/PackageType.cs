// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Newtonsoft.Json;

namespace NuGet.Protocol.Catalog
{
    /// <summary>
    /// Source: https://docs.microsoft.com/en-us/nuget/api/catalog-resource#catalog-leaf
    /// </summary>
    public class PackageType
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("version")]
        public string Version { get; set; }
    }
}
