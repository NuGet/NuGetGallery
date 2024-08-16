// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NuGet.Services.AzureSearch.SearchService
{
    /// <summary>
    /// Source: https://docs.microsoft.com/en-us/nuget/api/search-query-service-resource#search-result
    /// </summary>
    public class V3SearchPackage
    {
        [JsonPropertyName("@id")]
        public string AtId { get; set; }

        [JsonPropertyName("@type")]
        public string Type { get; set; }

        [JsonPropertyName("registration")]
        public string Registration { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("version")]
        public string Version { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("summary")]
        public string Summary { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("iconUrl")]
        public string IconUrl { get; set; }

        [JsonPropertyName("licenseUrl")]
        public string LicenseUrl { get; set; }

        [JsonPropertyName("projectUrl")]
        public string ProjectUrl { get; set; }

        [JsonPropertyName("tags")]
        public string[] Tags { get; set; }

        [JsonPropertyName("authors")]
        public string[] Authors { get; set; }

        [JsonPropertyName("owners")]
        public string[] Owners { get; set; }

        [JsonPropertyName("totalDownloads")]
        public long TotalDownloads { get; set; }

        [JsonPropertyName("verified")]
        public bool Verified { get; set; }

        [JsonPropertyName("packageTypes")]
        public List<V3SearchPackageType> PackageTypes { get; set; }

        [JsonPropertyName("versions")]
        public List<V3SearchVersion> Versions { get; set; }

        public object Debug { get; set; }

        [JsonPropertyName("deprecation")]
        public V3SearchDeprecation Deprecation { get; set; }

        [JsonPropertyName("vulnerabilities")]
        public List<V3SearchVulnerability> Vulnerabilities { get; set; }
    }
}
