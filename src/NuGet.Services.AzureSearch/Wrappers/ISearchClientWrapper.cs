// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;

namespace NuGet.Services.AzureSearch.Wrappers
{
    public interface ISearchClientWrapper
    {
        string IndexName { get; }
        Task<IndexDocumentsResult> IndexAsync<T>(IndexDocumentsBatch<T> batch) where T : class;
        Task<T> GetOrNullAsync<T>(string key) where T : class;
        Task<SingleSearchResultPage<T>> SearchAsync<T>(string searchText, SearchOptions options) where T : class;
        Task<long> CountAsync();
    }
}
