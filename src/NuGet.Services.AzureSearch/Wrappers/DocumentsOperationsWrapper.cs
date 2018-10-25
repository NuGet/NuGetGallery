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
    }
}
