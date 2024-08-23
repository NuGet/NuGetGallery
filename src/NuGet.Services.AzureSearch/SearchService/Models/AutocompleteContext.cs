﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Text.Json.Serialization;

namespace NuGet.Services.AzureSearch.SearchService
{
    public class AutocompleteContext
    {
        [JsonPropertyName("@vocab")]
        public string Vocab { get; set; }
    }
}
