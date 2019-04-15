// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Azure.Search;
using Microsoft.Azure.Search.Models;

namespace NuGet.Services.AzureSearch.Wrappers
{
    public class IndexesOperationsWrapper : IIndexesOperationsWrapper
    {
        private readonly IIndexesOperations _inner;

        public IndexesOperationsWrapper(IIndexesOperations inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public ISearchIndexClientWrapper GetClient(string indexName)
        {
            return new SearchIndexClientWrapper(_inner.GetClient(indexName));
        }

        public async Task<bool> ExistsAsync(string indexName)
        {
            return await _inner.ExistsAsync(indexName);
        }

        public async Task DeleteAsync(string indexName)
        {
            await _inner.DeleteAsync(indexName);
        }

        public async Task<Index> CreateAsync(Index index)
        {
            return await _inner.CreateAsync(index);
        }
    }
}
