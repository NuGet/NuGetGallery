// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace NuGet.Services.AzureSearch.SearchService
{
    public interface ISearchService
    {
        Task<V2SearchResponse> V2SearchAsync(V2SearchRequest request);
        Task<V3SearchResponse> V3SearchAsync(V3SearchRequest request);
    }
}