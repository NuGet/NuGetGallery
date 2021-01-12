// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace NuGet.Services.AzureSearch.SearchService
{
    /// <summary>
    /// Source: https://docs.microsoft.com/en-us/nuget/api/search-query-service-resource#search-result
    /// See the section about each item in the <c>versions</c> array.
    /// </summary>
    public class V3SearchVersion
    {
        [JsonProperty("version")]
        [JsonPropertyName("version")]
        public string Version { get; set; }

        [JsonProperty("downloads")]
        [JsonPropertyName("downloads")]
        public long Downloads { get; set; }

        [JsonProperty("@id")]
        [JsonPropertyName("@id")]
        public string AtId { get; set; }
    }
}
