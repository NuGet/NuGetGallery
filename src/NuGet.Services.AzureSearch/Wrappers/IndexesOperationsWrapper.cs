// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.Search;
using Microsoft.Azure.Search.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Rest.TransientFaultHandling;
using Index = Microsoft.Azure.Search.Models.Index;

namespace NuGet.Services.AzureSearch.Wrappers
{
    public class IndexesOperationsWrapper : IIndexesOperationsWrapper
    {
        private readonly IIndexesOperations _inner;
        private readonly DelegatingHandler[] _handlers;
        private readonly RetryPolicy _retryPolicy;
        private readonly ILogger<DocumentsOperationsWrapper> _documentsOperationsLogger;

        public IndexesOperationsWrapper(
            IIndexesOperations inner,
            DelegatingHandler[] handlers,
            RetryPolicy retryPolicy,
            ILogger<DocumentsOperationsWrapper> documentsOperationsLogger)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _handlers = handlers ?? throw new ArgumentNullException(nameof(handlers));
            _retryPolicy = retryPolicy;
            _documentsOperationsLogger = documentsOperationsLogger ?? throw new ArgumentNullException(nameof(documentsOperationsLogger));
        }

        /// <summary>
        /// This is implemented in lieu of <see cref="IndexesGetClientExtensions.GetClient(IIndexesOperations, string)"/>
        /// because it allows the delegating handlers and retry policy to be specified. See:
        /// https://github.com/Azure/azure-sdk-for-net/blob/96421089bc26198098f320ea50e0208e98376956/sdk/search/Microsoft.Azure.Search/src/IndexesGetClientExtensions.cs#L27-L41
        /// </summary>
        public ISearchIndexClientWrapper GetClient(string indexName)
        {
            var searchIndexClient = new SearchIndexClient(
                _inner.Client.SearchServiceName,
                indexName,
                _inner.Client.SearchCredentials,
                _inner.Client.HttpMessageHandlers.OfType<HttpClientHandler>().SingleOrDefault(),
                _handlers);

            searchIndexClient.SearchDnsSuffix = _inner.Client.SearchDnsSuffix;
            searchIndexClient.HttpClient.Timeout = _inner.Client.HttpClient.Timeout;

            if (_retryPolicy != null)
            {
                searchIndexClient.SetRetryPolicy(_retryPolicy);
            }

            return new SearchIndexClientWrapper(searchIndexClient, _documentsOperationsLogger);
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
