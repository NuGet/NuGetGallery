// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure.Search.Documents;

namespace NuGet.Services.AzureSearch.SearchService
{
    public interface ISearchParametersBuilder
    {
        SearchOptions LastCommitTimestamp();
        SearchOptions V2Search(V2SearchRequest request, bool isDefaultSearch);
        SearchOptions V3Search(V3SearchRequest request, bool isDefaultSearch);
        SearchOptions Autocomplete(AutocompleteRequest request, bool isDefaultSearch);
        SearchFilters GetSearchFilters(SearchRequest request);
    }
}