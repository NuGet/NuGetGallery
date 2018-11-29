// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;
using NuGet.Protocol.Catalog.Serialization;

namespace NuGet.Protocol.Catalog
{
    public class PackageDependency
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("range")]
        [JsonConverter(typeof(PackageDependencyRangeConverter))]
        public string Range { get; set; }
    }
}
