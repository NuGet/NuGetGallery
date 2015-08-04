// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.OData;
using System.Web.Http.OData.Query;
using NuGetGallery.Configuration;
using NuGetGallery.OData;
using NuGetGallery.OData.QueryInterceptors;
using QueryInterceptor;
using WebApi.OutputCache.V2;

// ReSharper disable once CheckNamespace
namespace NuGetGallery.Controllers
{
    public class ODataV2CuratedFeedController 
        : NuGetODataController
    {
        private const int MaxPageSize = 40;

        private readonly IEntitiesContext _entities;
        private readonly ConfigurationService _configurationService;
        private readonly ISearchService _searchService;
        private readonly ICuratedFeedService _curatedFeedService;

        private static readonly ODataQuerySettings SearchQuerySettings = new ODataQuerySettings 
        { 
             HandleNullPropagation = HandleNullPropagationOption.False,
             EnsureStableOrdering = true
        };

        public ODataV2CuratedFeedController(
            IEntitiesContext entities,
            ConfigurationService configurationService,
            ISearchService searchService,
            ICuratedFeedService curatedFeedService)
            : base(configurationService)
        {
            _entities = entities;
            _configurationService = configurationService;
            _searchService = searchService;
            _curatedFeedService = curatedFeedService;
        }

        // /api/v2/curated-feed/curatedFeedName/Packages
        [HttpGet]
        [HttpPost]
        [EnableQuery(PageSize = MaxPageSize, HandleNullPropagation = HandleNullPropagationOption.False, EnsureStableOrdering = true, AllowedQueryOptions = AllowedQueryOptions.All)]
        public IQueryable<V2FeedPackage> Get(string curatedFeedName)
        {
            if (!_entities.CuratedFeeds.Any(cf => cf.Name == curatedFeedName))
            {
                throw new HttpResponseException(HttpStatusCode.NotFound);
            }

            var packages = _curatedFeedService.GetPackages(curatedFeedName)
                .ToV2FeedPackageQuery(_configurationService.GetSiteRoot(UseHttps()), _configurationService.Features.FriendlyLicenses)
                .InterceptWith(new NormalizeVersionInterceptor());

            return packages;
        }

        // /api/v2/curated-feed/curatedFeedName/Packages/$count
        [HttpGet]
        public HttpResponseMessage GetCount(string curatedFeedName, ODataQueryOptions<V2FeedPackage> options)
        {
            var queryResults = (IQueryable<V2FeedPackage>)options.ApplyTo(Get(curatedFeedName));
            var count = queryResults.Count();

            return CountResult(count);
        }

        // /api/v2/curated-feed/curatedFeedName/Packages(Id=,Version=)
        [HttpGet]
        [CacheOutput(ServerTimeSpan = NuGetODataConfig.GetByIdAndVersionCacheTimeInSeconds)]
        [EnableQuery(PageSize = MaxPageSize, HandleNullPropagation = HandleNullPropagationOption.False, EnsureStableOrdering = true, AllowedQueryOptions = AllowedQueryOptions.All)]
        public async Task<IHttpActionResult> Get(string curatedFeedName, string id, string version)
        {
            return await GetCore(curatedFeedName, id, version);
        }

        // /api/v2/curated-feed/curatedFeedName/FindPackagesById()?id=
        [HttpGet]
        [HttpPost]
        [CacheOutput(ServerTimeSpan = NuGetODataConfig.GetByIdAndVersionCacheTimeInSeconds)]
        [EnableQuery(PageSize = MaxPageSize, HandleNullPropagation = HandleNullPropagationOption.False, EnsureStableOrdering = true, AllowedQueryOptions = AllowedQueryOptions.All)]
        public async Task<IHttpActionResult> FindPackagesById(string curatedFeedName, [FromODataUri]string id)
        {
            return await GetCore(curatedFeedName, id, null);
        }

        private async Task<IHttpActionResult> GetCore(string curatedFeedName, string id, string version)
        {
            if (!_entities.CuratedFeeds.Any(cf => cf.Name == curatedFeedName))
            {
                throw new HttpResponseException(HttpStatusCode.NotFound);
            }

            var packages = _curatedFeedService.GetPackages(curatedFeedName)
                .Where(p => p.PackageRegistration.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrEmpty(version))
            {
                packages = packages.Where(p => p.Version == version);
            }

            if (_configurationService.Features.PackageRestoreViaSearch)
            {
                try
                {
                    packages = await SearchAdaptor.FindByIdCore(
                        _searchService, GetTraditionalHttpContext().Request, packages, id, curatedFeed: null);
                }
                catch (Exception ex)
                {
                    // Swallowing Exception intentionally. If *anything* goes wrong in search, just fall back to the database.
                    // We don't want to break package restores. We do want to know if this happens, so here goes:
                    QuietLog.LogHandledException(ex);
                }
            }

            var query = packages.ToV2FeedPackageQuery(GetSiteRoot(), _configurationService.Features.FriendlyLicenses);
            return Ok(query);
        }

        // /api/v2/curated-feed/curatedFeedName/Packages(Id=,Version=)/propertyName
        [HttpGet]
        public IHttpActionResult GetPropertyFromPackages(string propertyName, string id, string version)
        {
            switch (propertyName.ToLowerInvariant())
            {
                case "id": return Ok(id);
                case "version": return Ok(version);
            }

            return BadRequest("Querying property " + propertyName + " is not supported.");
        }

        // /api/v2/curated-feed/curatedFeedName/Search()?searchTerm=&targetFramework=&includePrerelease=
        [HttpGet]
        [HttpPost]
        [CacheOutput(ServerTimeSpan = NuGetODataConfig.SearchCacheTimeInSeconds)]
        public async Task<IEnumerable<V2FeedPackage>> Search(
            string curatedFeedName, 
            ODataQueryOptions<V2FeedPackage> queryOptions,
            [FromODataUri] string searchTerm = "",
            [FromODataUri] string targetFramework = "", 
            [FromODataUri] bool includePrerelease = false)
        {
            if (!_entities.CuratedFeeds.Any(cf => cf.Name == curatedFeedName))
            {
                throw new HttpResponseException(HttpStatusCode.NotFound);
            }

            // Ensure we can provide paging
            var pageSize = queryOptions.Top != null ? (int?)null : MaxPageSize;
            var settings = new ODataQuerySettings(SearchQuerySettings) { PageSize = pageSize };

            // Handle OData-style |-separated list of frameworks.
            string[] targetFrameworkList = (targetFramework ?? "").Split(new[] { '\'', '|' }, StringSplitOptions.RemoveEmptyEntries);

            // For now, we'll just filter on the first one.
            if (targetFrameworkList.Length > 0)
            {
                // Until we support multiple frameworks, we need to prefer aspnet50 over aspnetcore50.
                if (targetFrameworkList.Contains("aspnet50"))
                {
                    targetFramework = "aspnet50";
                }
                else
                {
                    targetFramework = targetFrameworkList[0];
                }
            }

            // Peform actual search
            var curatedFeed = _curatedFeedService.GetFeedByName(curatedFeedName, includePackages: false);
            var packages = _curatedFeedService.GetPackages(curatedFeedName);

            // todo: search hijack should take queryOptions instead of manually parsing query options
            var query = await SearchAdaptor.SearchCore(
                _searchService, GetTraditionalHttpContext().Request, packages, searchTerm, targetFramework, includePrerelease, curatedFeed: curatedFeed);

            var totalHits = query.LongCount();
            var convertedQuery = query
                .ToV2FeedPackageQuery(GetSiteRoot(), _configurationService.Features.FriendlyLicenses);

            // apply OData query options + limit total of entries explicitly
            convertedQuery = (IQueryable<V2FeedPackage>)queryOptions.ApplyTo(
                convertedQuery.Take(pageSize ?? MaxPageSize)); 

            var nextLink = SearchAdaptor.GetNextLink(
                Request.RequestUri, convertedQuery, new { searchTerm, targetFramework, includePrerelease }, queryOptions, settings, false);

            return new PageResult<V2FeedPackage>(convertedQuery, nextLink, totalHits);
        }

        // /api/v2/curated-feed/curatedFeedName/Search()/$count?searchTerm=&targetFramework=&includePrerelease=
        [HttpGet]
        [CacheOutput(ServerTimeSpan = NuGetODataConfig.SearchCacheTimeInSeconds)]
        public async Task<HttpResponseMessage> SearchCount(
            string curatedFeedName, 
            ODataQueryOptions<V2FeedPackage> queryOptions,
            [FromODataUri] string searchTerm = "", 
            [FromODataUri] string targetFramework = "", 
            [FromODataUri] bool includePrerelease = false)
        {
            if (!_entities.CuratedFeeds.Any(cf => cf.Name == curatedFeedName))
            {
                throw new HttpResponseException(HttpStatusCode.NotFound);
            }

            var queryResults = await Search(curatedFeedName, queryOptions, searchTerm, targetFramework, includePrerelease);

            var pageResult = queryResults as PageResult;
            if (pageResult != null && pageResult.Count.HasValue)
            {
                return CountResult(pageResult.Count.Value);
            }

            return CountResult(queryResults.LongCount());
        }
    }
}