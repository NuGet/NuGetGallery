// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.Search.Models;
using NuGet.Services.AzureSearch.Wrappers;

namespace NuGet.Services.AzureSearch.Catalog2AzureSearch.Integration
{
    public class InMemoryDocumentsOperations : IDocumentsOperationsWrapper
    {
        public ConcurrentQueue<IndexBatch<KeyedDocument>> Batches { get; } = new ConcurrentQueue<IndexBatch<KeyedDocument>>();

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

        public Task<DocumentIndexResult> IndexAsync<T>(IndexBatch<T> batch) where T : class
        {
            if (typeof(T) != typeof(KeyedDocument))
            {
                throw new ArgumentException();
            }

            Batches.Enqueue(batch as IndexBatch<KeyedDocument>);

            return Task.FromResult(new DocumentIndexResult(new List<IndexingResult>()));
        }

        public Task<DocumentSearchResult<T>> SearchAsync<T>(string searchText, SearchParameters searchParameters) where T : class
        {
            throw new NotImplementedException();
        }

        public Task<DocumentSearchResult> SearchAsync(string searchText, SearchParameters searchParameters)
        {
            throw new NotImplementedException();
        }
    }
}
