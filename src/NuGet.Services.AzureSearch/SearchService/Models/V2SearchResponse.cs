// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NuGet.Services.AzureSearch.SearchService
{
    public class V2SearchResponse
    {
        [JsonPropertyName("totalHits")]
        public long TotalHits { get; set; }

        [JsonPropertyName("data")]
        public List<V2SearchPackage> Data { get; set; }

        public DebugInformation Debug { get; set; }
    }
}
