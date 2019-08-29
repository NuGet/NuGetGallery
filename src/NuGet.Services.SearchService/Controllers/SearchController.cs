// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Description;
using NuGet.Indexing;
using NuGet.Services.AzureSearch.SearchService;
using NuGet.Versioning;

namespace NuGet.Services.SearchService.Controllers
{
    public class SearchController : ApiController
    {
        private const int DefaultSkip = 0;
        private const int DefaultTake = SearchParametersBuilder.DefaultTake;

        private static readonly IReadOnlyDictionary<string, V2SortBy> SortBy = new Dictionary<string, V2SortBy>(StringComparer.OrdinalIgnoreCase)
        {
            { "lastEdited", V2SortBy.LastEditedDesc },
            { "published", V2SortBy.PublishedDesc },
            { "title-asc", V2SortBy.SortableTitleAsc },
            { "title-desc", V2SortBy.SortableTitleDesc },
            { "created-asc", V2SortBy.CreatedAsc },
            { "created-desc", V2SortBy.CreatedDesc },
        };

        private readonly IAuxiliaryDataCache _auxiliaryDataCache;
        private readonly ISearchService _searchService;
        private readonly ISearchStatusService _statusService;

        public SearchController(
            IAuxiliaryDataCache auxiliaryDataCache,
            ISearchService searchService,
            ISearchStatusService statusService)
        {
            _auxiliaryDataCache = auxiliaryDataCache ?? throw new ArgumentNullException(nameof(auxiliaryDataCache));
            _searchService = searchService ?? throw new ArgumentNullException(nameof(searchService));
            _statusService = statusService ?? throw new ArgumentNullException(nameof(statusService));
        }

        [HttpGet]
        [ResponseType(typeof(SearchStatusResponse))]
        public async Task<HttpResponseMessage> IndexAsync(HttpRequestMessage request)
        {
            var result = await GetStatusAsync(SearchStatusOptions.All);
            var statusCode = result.Success ? HttpStatusCode.OK : HttpStatusCode.InternalServerError;

            // Hide all information except the success boolean. This is the root page so we can keep it simple.
            result = new SearchStatusResponse
            {
                Success = result.Success,
            };

            return request.CreateResponse(statusCode, result);
        }

        [HttpGet]
        [ResponseType(typeof(SearchStatusResponse))]
        public async Task<HttpResponseMessage> GetStatusAsync(HttpRequestMessage request)
        {
            var result = await GetStatusAsync(SearchStatusOptions.All);
            var statusCode = result.Success ? HttpStatusCode.OK : HttpStatusCode.InternalServerError;

            return request.CreateResponse(statusCode, result);
        }

        [HttpGet]
        public async Task<V2SearchResponse> V2SearchAsync(
            int? skip = DefaultSkip,
            int? take = DefaultTake,
            bool? ignoreFilter = false,
            bool? countOnly = false,
            bool? prerelease = false,
            string semVerLevel = null,
            string q = null,
            string sortBy = null,
            bool? luceneQuery = true,
            bool? debug = false)
        {
            await EnsureInitializedAsync();

            var request = new V2SearchRequest
            {
                Skip = skip ?? DefaultSkip,
                Take = take ?? DefaultTake,
                IgnoreFilter = ignoreFilter ?? false,
                CountOnly = countOnly ?? false,
                IncludePrerelease = prerelease ?? false,
                IncludeSemVer2 = GetIncludeSemVer2(semVerLevel),
                Query = q,
                SortBy = GetSortBy(sortBy),
                LuceneQuery = luceneQuery ?? true,
                ShowDebug = debug ?? false,
            };

            return await _searchService.V2SearchAsync(request);
        }

        [HttpGet]
        public async Task<V3SearchResponse> V3SearchAsync(
            int? skip = DefaultSkip,
            int? take = DefaultTake,
            bool? prerelease = false,
            string semVerLevel = null,
            string q = null,
            bool? debug = false)
        {
            await EnsureInitializedAsync();

            var request = new V3SearchRequest
            {
                Skip = skip ?? DefaultSkip,
                Take = take ?? DefaultTake,
                IncludePrerelease = prerelease ?? false,
                IncludeSemVer2 = GetIncludeSemVer2(semVerLevel),
                Query = q,
                ShowDebug = debug ?? false,
            };

            return await _searchService.V3SearchAsync(request);
        }

        [HttpGet]
        public async Task<AutocompleteResponse> AutocompleteAsync(
            int? skip = DefaultSkip,
            int? take = DefaultTake,
            bool? prerelease = false,
            string semVerLevel = null,
            string q = null,
            string id = null,
            bool? debug = false)
        {
            await EnsureInitializedAsync();

            // If only "id" is provided, find package versions. Otherwise, find package Ids.
            var type = (q != null || id == null)
                ? AutocompleteRequestType.PackageIds
                : AutocompleteRequestType.PackageVersions;

            var request = new AutocompleteRequest
            {
                Skip = skip ?? DefaultSkip,
                Take = take ?? DefaultTake,
                IncludePrerelease = prerelease ?? false,
                IncludeSemVer2 = GetIncludeSemVer2(semVerLevel),
                Query = q ?? id,
                Type = type,
                ShowDebug = debug ?? false,
            };

            return await _searchService.AutocompleteAsync(request);
        }

        private async Task EnsureInitializedAsync()
        {
            /// Ensure the auxiliary data is loaded before processing a request. This is necessary because the response
            /// builder depends on <see cref="IAuxiliaryDataCache.Get" />, which requires that the auxiliary files have
            /// been loaded at least once.
            await _auxiliaryDataCache.EnsureInitializedAsync();
        }

        private async Task<SearchStatusResponse> GetStatusAsync(SearchStatusOptions options)
        {
            var assemblyForMetadata = typeof(SearchController).Assembly;
            return await _statusService.GetStatusAsync(options, assemblyForMetadata);
        }

        private static V2SortBy GetSortBy(string sortBy)
        {
            if (sortBy == null || !SortBy.TryGetValue(sortBy, out var parsedSortBy))
            {
                parsedSortBy = V2SortBy.Popularity;
            }

            return parsedSortBy;
        }

        private static bool GetIncludeSemVer2(string semVerLevel)
        {
            if (!NuGetVersion.TryParse(semVerLevel, out var semVerLevelVersion))
            {
                return false;
            }
            else
            {
                return SemVerHelpers.ShouldIncludeSemVer2Results(semVerLevelVersion);
            }
        }
    }
}
