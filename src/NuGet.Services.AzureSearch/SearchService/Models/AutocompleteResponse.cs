// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Newtonsoft.Json;

namespace NuGet.Services.AzureSearch.SearchService
{
    /// <summary>
    /// Source: https://docs.microsoft.com/en-us/nuget/api/search-autocomplete-service-resource#response
    /// </summary>
    public class AutocompleteResponse
    {
        [JsonProperty("@context")]
        public AutocompleteContext Context { get; set; }

        [JsonProperty("totalHits")]
        public long TotalHits { get; set; }

        [JsonProperty("data")]
        public List<string> Data { get; set; }

        public DebugInformation Debug { get; set; }
    }
}
