// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace NuGet.Services.AzureSearch.SearchService
{
    public class V3SearchContext
    {
        [JsonProperty("@vocab")]
        [JsonPropertyName("@vocab")]
        public string Vocab { get; set; }

        [JsonProperty("@base")]
        [JsonPropertyName("@base")]
        public string Base { get; set; }
    }
}
