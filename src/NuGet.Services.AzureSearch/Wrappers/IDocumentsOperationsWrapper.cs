// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.Azure.Search.Models;

namespace NuGet.Services.AzureSearch.Wrappers
{
    public interface IDocumentsOperationsWrapper
    {
        Task<DocumentIndexResult> IndexAsync<T>(IndexBatch<T> batch) where T : class;
        Task<T> GetOrNullAsync<T>(string key) where T : class;
        Task<DocumentSearchResult> SearchAsync(
            string searchText,
            SearchParameters searchParameters);
        Task<DocumentSearchResult<T>> SearchAsync<T>(
            string searchText,
            SearchParameters searchParameters) where T : class;
        Task<long> CountAsync();
    }
}
