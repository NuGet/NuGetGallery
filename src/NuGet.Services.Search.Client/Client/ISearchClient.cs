// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Services.Search.Models;

namespace NuGet.Services.Search.Client
{
    public interface ISearchClient
    {
        Task<ServiceResponse<SearchResults>> Search(
            string query,
            string projectTypeFilter,
            bool includePrerelease,
            SortOrder sortBy,
            int skip,
            int take,
            bool isLuceneQuery,
            bool countOnly,
            bool explain,
            bool getAllVersions,
            string supportedFramework,
            string semVerLevel);

        Task<ServiceResponse<JObject>> GetDiagnostics();
    }
}
