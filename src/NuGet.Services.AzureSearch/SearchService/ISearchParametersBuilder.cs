// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Azure.Search.Models;

namespace NuGet.Services.AzureSearch.SearchService
{
    public interface ISearchParametersBuilder
    {
        SearchParameters GetSearchParametersForV2Search(V2SearchRequest request);
        SearchParameters GetSearchParametersForV3Search(V3SearchRequest request);
        string GetSearchTextForV2Search(V2SearchRequest request);
        string GetSearchTextForV3Search(V3SearchRequest request);
    }
}