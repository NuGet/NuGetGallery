// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Newtonsoft.Json;

namespace NuGet.Protocol.Catalog
{
    public class CatalogPageItem
    {
        [JsonProperty("@id")]
        public string Url { get; set; }

        [JsonProperty("commitTimeStamp")]
        public DateTimeOffset CommitTimestamp { get; set; }

        [JsonProperty("count")]
        public int Count { get; set; }
    }
}
