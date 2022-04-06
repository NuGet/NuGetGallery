// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;

namespace NuGet.Services.AzureSearch.Wrappers
{
    public class SearchIndexClientWrapper : ISearchIndexClientWrapper
    {
        private readonly SearchIndexClient _inner;

        public SearchIndexClientWrapper(SearchIndexClient inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public ISearchClientWrapper GetSearchClient(string indexName)
        {
            return new SearchClientWrapper(_inner.GetSearchClient(indexName));
        }

        public async Task<SearchIndex> GetIndexAsync(string indexName)
        {
            return await _inner.GetIndexAsync(indexName);
        }

        public async Task DeleteIndexAsync(string indexName)
        {
            await _inner.DeleteIndexAsync(indexName);
        }

        public async Task CreateIndexAsync(SearchIndex index)
        {
            await _inner.CreateIndexAsync(index);
        }
    }
}
