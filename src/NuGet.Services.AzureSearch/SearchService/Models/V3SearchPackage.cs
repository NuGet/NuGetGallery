// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace NuGet.Services.AzureSearch.SearchService
{
    /// <summary>
    /// Source: https://docs.microsoft.com/en-us/nuget/api/search-query-service-resource#search-result
    /// </summary>
    public class V3SearchPackage
    {
        [JsonProperty("@id")]
        [JsonPropertyName("@id")]
        public string AtId { get; set; }

        [JsonProperty("@type")]
        [JsonPropertyName("@type")]
        public string Type { get; set; }

        [JsonProperty("registration")]
        [JsonPropertyName("registration")]
        public string Registration { get; set; }

        [JsonProperty("id")]
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonProperty("version")]
        [JsonPropertyName("version")]
        public string Version { get; set; }

        [JsonProperty("description")]
        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonProperty("summary")]
        [JsonPropertyName("summary")]
        public string Summary { get; set; }

        [JsonProperty("title")]
        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonProperty("iconUrl")]
        [JsonPropertyName("iconUrl")]
        public string IconUrl { get; set; }

        [JsonProperty("licenseUrl")]
        [JsonPropertyName("licenseUrl")]
        public string LicenseUrl { get; set; }

        [JsonProperty("projectUrl")]
        [JsonPropertyName("projectUrl")]
        public string ProjectUrl { get; set; }

        [JsonProperty("tags")]
        [JsonPropertyName("tags")]
        public string[] Tags { get; set; }

        [JsonProperty("authors")]
        [JsonPropertyName("authors")]
        public string[] Authors { get; set; }

        [JsonProperty("owners")]
        [JsonPropertyName("owners")]
        public string[] Owners { get; set; }

        [JsonProperty("totalDownloads")]
        [JsonPropertyName("totalDownloads")]
        public long TotalDownloads { get; set; }

        [JsonProperty("verified")]
        [JsonPropertyName("verified")]
        public bool Verified { get; set; }

        [JsonProperty("packageTypes")]
        [JsonPropertyName("packageTypes")]
        public List<V3SearchPackageType> PackageTypes { get; set; }

        [JsonProperty("versions")]
        [JsonPropertyName("versions")]
        public List<V3SearchVersion> Versions { get; set; }

        public object Debug { get; set; }
    }
}
