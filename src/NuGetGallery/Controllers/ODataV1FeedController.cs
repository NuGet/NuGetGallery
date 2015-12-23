﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.OData;
using System.Web.Http.OData.Query;
using NuGetGallery.Configuration;
using NuGetGallery.Infrastructure;
using NuGetGallery.OData;
using NuGetGallery.WebApi;
using WebApi.OutputCache.V2;

// ReSharper disable once CheckNamespace
namespace NuGetGallery.Controllers
{
    public class ODataV1FeedController 
        : NuGetODataController
    {
        private const int MaxPageSize = SearchAdaptor.MaxPageSize;

        private readonly IEntityRepository<Package> _packagesRepository;
        private readonly ConfigurationService _configurationService;
        private readonly ISearchService _searchService;

        public ODataV1FeedController(
            IEntityRepository<Package> packagesRepository, 
            ConfigurationService configurationService, 
            ISearchService searchService)
            : base(configurationService)
        {
            _packagesRepository = packagesRepository;
            _configurationService = configurationService;
            _searchService = searchService;
        }

        // /api/v1/Packages
        [HttpGet]
        [HttpPost]
        [CacheOutput(NoCache = true)]
        public IHttpActionResult Get(ODataQueryOptions<V1FeedPackage> options)
        {
            var queryable = _packagesRepository.GetAll()
                .Where(p => !p.IsPrerelease && !p.Deleted)
                .WithoutVersionSort()
                .ToV1FeedPackageQuery(_configurationService.GetSiteRoot(UseHttps()));

            return QueryResult(options, queryable, MaxPageSize);
        }

        // /api/v1/Packages/$count
        [HttpGet]
        [CacheOutput(NoCache = true)]
        public IHttpActionResult GetCount(ODataQueryOptions<V1FeedPackage> options)
        {
            return Get(options).FormattedAsCountResult<V1FeedPackage>();
        }

        // /api/v1/Packages(Id=,Version=)
        [HttpGet]
        [CacheOutput(ServerTimeSpan = NuGetODataConfig.GetByIdAndVersionCacheTimeInSeconds, Private = true, ClientTimeSpan = NuGetODataConfig.GetByIdAndVersionCacheTimeInSeconds)]
        public async Task<IHttpActionResult> Get(ODataQueryOptions<V1FeedPackage> options, string id, string version)
        {
            var result = await GetCore(options, id, version, return404NotFoundWhenNoResults: true);
            return result.FormattedAsSingleResult<V1FeedPackage>();
        }

        // /api/v1/FindPackagesById()?id=
        [HttpGet]
        [HttpPost]
        [CacheOutput(ServerTimeSpan = NuGetODataConfig.GetByIdAndVersionCacheTimeInSeconds, Private = true, ClientTimeSpan = NuGetODataConfig.GetByIdAndVersionCacheTimeInSeconds)]
        public async Task<IHttpActionResult> FindPackagesById(ODataQueryOptions<V1FeedPackage> options, [FromODataUri]string id)
        {
            return await GetCore(options, id, version: null, return404NotFoundWhenNoResults: false);
        }

        private async Task<IHttpActionResult> GetCore(ODataQueryOptions<V1FeedPackage> options, string id, string version, bool return404NotFoundWhenNoResults)
        {
            var packages = _packagesRepository.GetAll()
                .Include(p => p.PackageRegistration)
                .Where(p => p.PackageRegistration.Id.Equals(id, StringComparison.OrdinalIgnoreCase) && !p.IsPrerelease && !p.Deleted);

            if (!string.IsNullOrEmpty(version))
            {
                packages = packages.Where(p => p.Version == version);
            }

            // try the search service
            try
            {
                packages = await SearchAdaptor.FindByIdAndVersionCore(
                    _searchService, GetTraditionalHttpContext().Request, packages, id, version, curatedFeed: null);
            }
            catch (Exception ex)
            {
                // Swallowing Exception intentionally. If *anything* goes wrong in search, just fall back to the database.
                // We don't want to break package restores. We do want to know if this happens, so here goes:
                QuietLog.LogHandledException(ex);
            }

            if (return404NotFoundWhenNoResults && !packages.Any())
            {
                return NotFound();
            }

            var queryable = packages.ToV1FeedPackageQuery(GetSiteRoot());
            return QueryResult(options, queryable, MaxPageSize);
        }

        // /api/v1/Packages(Id=,Version=)/propertyName
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

        // /api/v1/Search()?searchTerm=&targetFramework=&includePrerelease=
        [HttpGet]
        [HttpPost]
        [CacheOutput(ServerTimeSpan = NuGetODataConfig.SearchCacheTimeInSeconds, ClientTimeSpan = NuGetODataConfig.SearchCacheTimeInSeconds)]
        public async Task<IHttpActionResult> Search(
            ODataQueryOptions<V1FeedPackage> options,
            [FromODataUri]string searchTerm = "", 
            [FromODataUri]string targetFramework = "")
        {
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
            var packages = _packagesRepository.GetAll()
                .Include(p => p.PackageRegistration)
                .Include(p => p.PackageRegistration.Owners)
                .Where(p => p.Listed && !p.IsPrerelease && !p.Deleted)
                .OrderBy(p => p.PackageRegistration.Id).ThenBy(p => p.Version)
                .AsNoTracking();

            // todo: search hijack should take queryOptions instead of manually parsing query options
            var query = await SearchAdaptor.SearchCore(
                _searchService, GetTraditionalHttpContext().Request, packages, searchTerm, targetFramework, false, curatedFeed: null);
            
            // If intercepted by SearchAdaptor, create a paged queryresult
            if (query.IsQueryTranslator())
            {
                // Add explicit Take() needed to limit search hijack result set size if $top is specified
                var totalHits = query.LongCount();
                var pagedQueryable = query
                    .Take(options.Top != null ? Math.Min(options.Top.Value, MaxPageSize) : MaxPageSize)
                    .ToV1FeedPackageQuery(GetSiteRoot());

                return QueryResult(options, pagedQueryable, MaxPageSize, totalHits, (o, s, resultCount) =>
                   SearchAdaptor.GetNextLink(Request.RequestUri, resultCount, new { searchTerm, targetFramework }, o, s));
            }

            // If not, just let OData handle things
            var queryable = query.ToV1FeedPackageQuery(GetSiteRoot());
            return QueryResult(options, queryable, MaxPageSize);
        }

        // /api/v1/Search()/$count?searchTerm=&targetFramework=&includePrerelease=
        [HttpGet]
        [CacheOutput(ServerTimeSpan = NuGetODataConfig.SearchCacheTimeInSeconds, ClientTimeSpan = NuGetODataConfig.SearchCacheTimeInSeconds)]
        public async Task<IHttpActionResult> SearchCount(
            ODataQueryOptions<V1FeedPackage> options, 
            [FromODataUri]string searchTerm = "",
            [FromODataUri]string targetFramework = "")
        {
            var searchResults = await Search(options, searchTerm, targetFramework);
            return searchResults.FormattedAsCountResult<V1FeedPackage>();
        }
    }
}