// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Azure.Search.Models;

namespace NuGet.Services.AzureSearch.SearchService
{
    public interface ISearchParametersBuilder
    {
        SearchParameters LastCommitTimestamp();
        SearchParameters V2Search(V2SearchRequest request, bool isDefaultSearch);
        SearchParameters V3Search(V3SearchRequest request, bool isDefaultSearch);
        SearchParameters Autocomplete(AutocompleteRequest request, bool isDefaultSearch);
        SearchFilters GetSearchFilters(SearchRequest request);
    }
}