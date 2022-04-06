// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Text.Json.Serialization;

namespace NuGet.Services.AzureSearch.SearchService
{
    /// <summary>
    /// Source: https://docs.microsoft.com/en-us/nuget/api/search-query-service-resource#search-result
    /// See the section about each item in the <c>versions</c> array.
    /// </summary>
    public class V3SearchVersion
    {
        [JsonPropertyName("version")]
        public string Version { get; set; }

        [JsonPropertyName("downloads")]
        public long Downloads { get; set; }

        [JsonPropertyName("@id")]
        public string AtId { get; set; }
    }
}
