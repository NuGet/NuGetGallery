// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using NuGet.Services.AzureSearch.Wrappers;

namespace NuGet.Services.AzureSearch.Catalog2AzureSearch.Integration
{
    public class InMemorySearchClient : ISearchClientWrapper
    {
        public InMemorySearchClient(string indexName)
        {
            IndexName = indexName;
        }

        public ConcurrentQueue<IndexDocumentsBatch<KeyedDocument>> Batches { get; } = new ConcurrentQueue<IndexDocumentsBatch<KeyedDocument>>();

        public string IndexName { get; set; }

        public void Clear()
        {
            while (Batches.Count > 0)
            {
                Batches.TryDequeue(out var _);
            }
        }

        public Task<long> CountAsync()
        {
            throw new NotImplementedException();
        }

        public Task<T> GetOrNullAsync<T>(string key) where T : class
        {
            throw new NotImplementedException();
        }

        public Task<IndexDocumentsResult> IndexAsync<T>(IndexDocumentsBatch<T> batch) where T : class
        {
            if (typeof(T) != typeof(KeyedDocument))
            {
                throw new ArgumentException();
            }

            Batches.Enqueue(batch as IndexDocumentsBatch<KeyedDocument>);

            return Task.FromResult(SearchModelFactory.IndexDocumentsResult(new List<IndexingResult>()));
        }

        public Task<SingleSearchResultPage<T>> SearchAsync<T>(string searchText, SearchOptions searchParameters) where T : class
        {
            throw new NotImplementedException();
        }
    }
}
