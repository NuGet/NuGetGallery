// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.Azure.Search.Models;

namespace NuGet.Services.AzureSearch.Wrappers
{
    public interface IIndexesOperationsWrapper
    {
        ISearchIndexClientWrapper GetClient(string indexName);
        Task<Index> CreateAsync(Index index);
        Task DeleteAsync(string indexName);
        Task<bool> ExistsAsync(string indexName);
    }
}
