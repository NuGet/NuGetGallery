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
            string text,
            SearchParameters parameters,
            DocumentSearchResult<HijackDocument.Full> result,
            TimeSpan duration);
        V2SearchResponse V2FromSearch(
            V2SearchRequest request,
            string text,
            SearchParameters parameters,
            DocumentSearchResult<SearchDocument.Full> result,
            TimeSpan duration);
        V2SearchResponse V2FromHijackDocument(
            V2SearchRequest request,
            string documentKey,
            HijackDocument.Full document,
            TimeSpan duration);
        V3SearchResponse V3FromSearch(
            V3SearchRequest request,
            string text,
            SearchParameters parameters,
            DocumentSearchResult<SearchDocument.Full> result,
            TimeSpan duration);
        V2SearchResponse V2FromSearchDocument(
            V2SearchRequest request,
            string documentKey,
            SearchDocument.Full document,
            TimeSpan duration);
        V3SearchResponse V3FromSearchDocument(
            V3SearchRequest request,
            string documentKey,
            SearchDocument.Full document,
            TimeSpan duration);
        AutocompleteResponse AutocompleteFromSearch(
            AutocompleteRequest request,
            string text,
            SearchParameters parameters,
            DocumentSearchResult<SearchDocument.Full> result,
            TimeSpan duration);
        V2SearchResponse EmptyV2(V2SearchRequest request);
        V3SearchResponse EmptyV3(V3SearchRequest request);
        AutocompleteResponse EmptyAutocomplete(AutocompleteRequest request);
    }
}