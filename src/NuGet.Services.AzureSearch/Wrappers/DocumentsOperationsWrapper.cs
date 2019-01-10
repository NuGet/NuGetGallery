// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Azure.Search;
using Microsoft.Azure.Search.Models;

namespace NuGet.Services.AzureSearch.Wrappers
{
    public class DocumentsOperationsWrapper : IDocumentsOperationsWrapper
    {
        private readonly IDocumentsOperations _inner;

        public DocumentsOperationsWrapper(IDocumentsOperations inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public async Task<DocumentIndexResult> IndexAsync<T>(IndexBatch<T> batch) where T : class
        {
            return await _inner.IndexAsync(batch);
        }

        public async Task<DocumentSearchResult> SearchAsync(string searchText, SearchParameters searchParameters)
        {
            return await _inner.SearchAsync(searchText, searchParameters);
        }

        public async Task<DocumentSearchResult<T>> SearchAsync<T>(string searchText, SearchParameters searchParameters) where T : class
        {
            return await _inner.SearchAsync<T>(searchText, searchParameters);
        }

        public async Task<long> CountAsync()
        {
            return await _inner.CountAsync();
        }
    }
}
