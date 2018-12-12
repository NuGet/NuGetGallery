// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;

namespace NuGet.Services.AzureSearch.SearchService
{
    public class V3SearchContext
    {
        [JsonProperty("@vocab")]
        public string Vocab { get; set; }

        [JsonProperty("@base")]
        public string Base { get; set; }
    }
}
