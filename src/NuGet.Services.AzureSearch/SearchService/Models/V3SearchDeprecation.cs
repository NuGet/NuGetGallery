// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Text.Json.Serialization;

namespace NuGet.Services.AzureSearch.SearchService
{
    public class V3SearchDeprecation
    {
        [JsonPropertyName("alternatePackage")]
        public V3SearchAlternatePackage AlternatePackage { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; }

        [JsonPropertyName("reasons")]
        public string[] Reasons { get; set; }
    }

    public class V3SearchAlternatePackage
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("range")]
        public string Range { get; set; }
    }
}