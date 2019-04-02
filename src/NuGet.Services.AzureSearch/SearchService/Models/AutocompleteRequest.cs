// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.AzureSearch.SearchService
{
    /// <summary>
    /// Source: https://docs.microsoft.com/en-us/nuget/api/search-autocomplete-service-resource#request-parameters
    /// </summary>
    public class AutocompleteRequest : SearchRequest
    {
        public AutocompleteRequestType Type { get; set; }
    }
}
