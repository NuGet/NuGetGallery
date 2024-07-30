// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Azure.Search.Documents.Indexes.Models;

namespace NuGet.Services.AzureSearch.Wrappers
{
    public interface ISearchIndexClientWrapper
    {
        ISearchClientWrapper GetSearchClient(string indexName);
        Task<SearchIndex> GetIndexAsync(string indexName);
        Task DeleteIndexAsync(string indexName);
        Task CreateIndexAsync(SearchIndex index);
    }
}
