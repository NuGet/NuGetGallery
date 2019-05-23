// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.Search.Models;

namespace NuGet.Services.AzureSearch.SearchService
{
    public interface ISearchResponseBuilder
    {
        V2SearchResponse V2FromHijack(
            V2SearchRequest request,
            SearchParameters parameters,
            string text,
            DocumentSearchResult<HijackDocument.Full> result,
            TimeSpan duration);
        V2SearchResponse V2FromSearch(
            V2SearchRequest request,
            SearchParameters parameters,
            string text,
            DocumentSearchResult<SearchDocument.Full> result,
            TimeSpan duration);
        V3SearchResponse V3FromSearch(
            V3SearchRequest request,
            SearchParameters parameters,
            string text,
            DocumentSearchResult<SearchDocument.Full> result,
            TimeSpan duration);
        AutocompleteResponse AutocompleteFromSearch(
            AutocompleteRequest request,
            SearchParameters parameters,
            string text,
            DocumentSearchResult<SearchDocument.Full> result,
            TimeSpan duration);
    }
}