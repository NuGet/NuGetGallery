// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Azure.Search;
using Microsoft.Azure.Search.Models;
using Microsoft.Extensions.Logging;

namespace NuGet.Services.AzureSearch.Wrappers
{
    public class DocumentsOperationsWrapper : IDocumentsOperationsWrapper
    {
        private readonly IDocumentsOperations _inner;
        private readonly ILogger<DocumentsOperationsWrapper> _logger;

        public DocumentsOperationsWrapper(
            IDocumentsOperations inner,
            ILogger<DocumentsOperationsWrapper> logger)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<DocumentIndexResult> IndexAsync<T>(IndexBatch<T> batch) where T : class
        {
            return await RetryAsync(nameof(IndexAsync), async () => await _inner.IndexAsync(batch));
        }

        public async Task<DocumentSearchResult> SearchAsync(string searchText, SearchParameters searchParameters)
        {
            return await RetryAsync(nameof(SearchAsync), async () => await _inner.SearchAsync(searchText, searchParameters));
        }

        public async Task<DocumentSearchResult<T>> SearchAsync<T>(string searchText, SearchParameters searchParameters) where T : class
        {
            return await RetryAsync(nameof(SearchAsync) + "<T>", async() => await _inner.SearchAsync<T>(searchText, searchParameters));
        }

        public async Task<long> CountAsync()
        {
            return await RetryAsync(nameof(CountAsync), async () => await _inner.CountAsync());
        }

        private async Task<T> RetryAsync<T>(string name, Func<Task<T>> actAsync)
        {
            const int maxAttempts = 3;
            int currentAttempt = 0;
            while (true)
            {
                currentAttempt++;
                try
                {
                    return await actAsync();
                }
                catch (NullReferenceException ex)
                {
                    if (currentAttempt < maxAttempts)
                    {
                        _logger.LogWarning(
                            "Operation {Name} failed attempt {CurrentAttempt} with a null reference exception. " +
                            "Retrying.",
                            name,
                            currentAttempt);
                        continue;
                    }

                    // There is a bug where an inner RetryDelegatingHandler fails with NullReferenceException when the
                    // service is under load and attempting a search. Throw a clearer exception for now so we can
                    // understand the frequency in logs.
                    // https://github.com/Azure/azure-sdk-for-net/issues/3224
                    // https://github.com/NuGet/Engineering/issues/2511
                    throw new AzureSearchException("The search query failed due to Azure/azure-sdk-for-net#3224.", ex);
                }
                catch (Exception ex)
                {
                    throw new AzureSearchException($"Operation {name} failed.", ex);
                }
            }
        }
    }
}
