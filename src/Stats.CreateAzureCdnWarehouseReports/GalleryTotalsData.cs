// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Newtonsoft.Json;

namespace Stats.CreateAzureCdnWarehouseReports
{
    public class GalleryTotalsData
    {
        [JsonProperty("downloads")]
        public long Downloads { get; set; }

        [JsonProperty("uniquePackages")]
        public int UniquePackages { get; set; }

        [JsonProperty("totalPackages")]
        public int TotalPackages { get; set; }

        [JsonProperty("lastUpdateDateUtc")]
        public DateTime? LastUpdateDateUtc { get; set; }
    }
}