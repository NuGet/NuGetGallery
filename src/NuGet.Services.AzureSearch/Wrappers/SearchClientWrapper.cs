// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Threading.Tasks;
using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using NuGet.Services.AzureSearch.SearchService;

namespace NuGet.Services.AzureSearch.Wrappers
{
    public class SearchClientWrapper : ISearchClientWrapper
    {
        private readonly SearchClient _inner;

        public SearchClientWrapper(SearchClient inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public string IndexName => _inner.IndexName;

        public async Task<IndexDocumentsResult> IndexAsync<T>(IndexDocumentsBatch<T> batch) where T : class
        {
            return await _inner.IndexDocumentsAsync(batch);
        }

        public async Task<T> GetOrNullAsync<T>(string key) where T : class
        {
            return await ExecuteAsync<T>(
                nameof(GetOrNullAsync) + "<T>",
                async () => await _inner.GetDocumentAsync<T>(key),
                allow404: true);
        }

        public async Task<SingleSearchResultPage<T>> SearchAsync<T>(string searchText, SearchOptions options) where T : class
        {
            return await ExecuteAsync(
                nameof(SearchAsync) + "<T>",
                async () =>
                {
                    SearchResults<T> results = await _inner.SearchAsync<T>(searchText, options);

                    var enumerator = results.GetResultsAsync().AsPages().GetAsyncEnumerator();
                    try
                    {
                        while (await enumerator.MoveNextAsync())
                        {
                            // Only read the first page. We use paging parameters through the application to avoid paging.
                            return new SingleSearchResultPage<T>(enumerator.Current.Values, results.TotalCount);
                        }
                    }
                    finally
                    {
                        await enumerator.DisposeAsync();
                    }

                    return new SingleSearchResultPage<T>(Array.Empty<SearchResult<T>>(), results.TotalCount);
                },
                allow404: false);
        }

        public async Task<long> CountAsync()
        {
            return await ExecuteAsync(
                nameof(CountAsync),
                async () => await _inner.GetDocumentCountAsync(),
                allow404: false);
        }

        private async Task<T> ExecuteAsync<T>(string name, Func<Task<T>> actAsync, bool allow404)
        {
            try
            {
                return await actAsync();
            }
            catch (RequestFailedException ex) when (allow404 && ex.Status == (int)HttpStatusCode.NotFound)
            {
                return default(T);
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.BadRequest)
            {
                throw new InvalidSearchRequestException("The provided query is invalid.", ex);
            }
            catch (Exception ex)
            {
                throw new AzureSearchException($"Operation {name} failed.", ex);
            }
        }
    }
}
