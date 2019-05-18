// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SearchModels = NuGetGallery.Infrastructure.Search.Models;

namespace NuGetGallery.Infrastructure.Search
{
    public interface ISearchClient
    {
        /// <summary>
        /// Performs the Search based on the parameters.
        /// </summary>
        /// <param name="query">The query string.</param>
        /// <param name="projectTypeFilter">ProjectType</param>
        /// <param name="includePrerelease">IncludePrerelease</param>
        /// <param name="sortBy">SortBy</param>
        /// <param name="skip">Skip</param>
        /// <param name="take">Take</param>
        /// <param name="isLuceneQuery">IsLuceneQuery</param>
        /// <param name="countOnly">CountOnly</param>
        /// <param name="explain">Explain</param>
        /// <param name="getAllVersions">GetAllVersions</param>
        /// <param name="supportedFramework">SupportedFramework</param>
        /// <param name="semVerLevel">SemVerLevel</param>
        /// <returns></returns>
        Task<ServiceResponse<SearchModels.SearchResults>> Search(
            string query,
            string projectTypeFilter,
            bool includePrerelease,
            SearchModels.SortOrder sortBy,
            int skip,
            int take,
            bool isLuceneQuery,
            bool countOnly,
            bool explain,
            bool getAllVersions,
            string supportedFramework,
            string semVerLevel);

        /// <summary>
        /// Returns the search diag.
        /// </summary>
        /// <returns></returns>
        Task<ServiceResponse<JObject>> GetDiagnostics();
    }
}
