// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Mvc;
using NuGet.Services.AzureSearch.SearchService;
using System;
using System.Net;
using System.Threading.Tasks;

namespace NuGet.Services.SearchService.Controllers
{
    [ApiController]
    public class SearchController : ControllerBase
    {
        private const int DefaultSkip = 0;
        private const int DefaultTake = SearchParametersBuilder.DefaultTake;

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
        [Route("/")]
        public async Task<ActionResult<SearchStatusResponse>> IndexAsync()
        {
            var result = await GetStatusAsync(SearchStatusOptions.All);
            var statusCode = result.Success ? HttpStatusCode.OK : HttpStatusCode.InternalServerError;

            // Hide all information except the success boolean. This is the root page so we can keep it simple.
            result = new SearchStatusResponse
            {
                Success = result.Success,
            };

            return new JsonResult(result) { StatusCode = (int)statusCode };
        }

        [HttpGet]
        [Route("/search/diag")]
        public async Task<ActionResult<SearchStatusResponse>> GetStatusAsync()
        {
            var result = await GetStatusAsync(SearchStatusOptions.All);
            var statusCode = result.Success ? HttpStatusCode.OK : HttpStatusCode.InternalServerError;

            return new JsonResult(result) { StatusCode = (int)statusCode };
        }

        [HttpGet]
        [Route("/search/query")]
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
            string packageType = null,
            bool? testData = false,
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
                IncludeSemVer2 = ParameterUtilities.ParseIncludeSemVer2(semVerLevel),
                Query = q,
                SortBy = ParameterUtilities.ParseV2SortBy(sortBy),
                LuceneQuery = luceneQuery ?? true,
                PackageType = packageType,
                IncludeTestData = testData ?? false,
                ShowDebug = debug ?? false,
            };

            return await _searchService.V2SearchAsync(request);
        }

        [HttpGet]
        [Route("/query")]
        public async Task<V3SearchResponse> V3SearchAsync(
            int? skip = DefaultSkip,
            int? take = DefaultTake,
            bool? prerelease = false,
            string semVerLevel = null,
            string q = null,
            string packageType = null,
            bool? testData = false,
            bool? debug = false)
        {
            await EnsureInitializedAsync();

            var request = new V3SearchRequest
            {
                Skip = skip ?? DefaultSkip,
                Take = take ?? DefaultTake,
                IncludePrerelease = prerelease ?? false,
                IncludeSemVer2 = ParameterUtilities.ParseIncludeSemVer2(semVerLevel),
                Query = q,
                PackageType = packageType,
                IncludeTestData = testData ?? false,
                ShowDebug = debug ?? false,
            };

            return await _searchService.V3SearchAsync(request);
        }

        [HttpGet]
        [Route("/autocomplete")]
        public async Task<AutocompleteResponse> AutocompleteAsync(
            int? skip = DefaultSkip,
            int? take = DefaultTake,
            bool? prerelease = false,
            string semVerLevel = null,
            string q = null,
            string id = null,
            string packageType = null,
            bool? testData = false,
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
                IncludeSemVer2 = ParameterUtilities.ParseIncludeSemVer2(semVerLevel),
                Query = q ?? id,
                Type = type,
                PackageType = packageType,
                IncludeTestData = testData ?? false,
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
    }
}
