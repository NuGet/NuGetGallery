// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.OData;
using System.Web.Http.OData.Query;
using NuGet.Services.Entities;
using NuGetGallery.Configuration;
using NuGetGallery.OData;
using NuGetGallery.OData.QueryFilter;
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
        private readonly IGalleryConfigurationService _configurationService;
        private readonly ISearchService _searchService;
        private readonly IIconUrlProvider _iconUrlProvider;

        public ODataV1FeedController(
            IEntityRepository<Package> packagesRepository,
            IGalleryConfigurationService configurationService,
            ISearchService searchService,
            ITelemetryService telemetryService,
            IIconUrlProvider iconUrlProvider)
            : base(configurationService, telemetryService)
        {
            _packagesRepository = packagesRepository;
            _configurationService = configurationService;
            _searchService = searchService;
            _iconUrlProvider = iconUrlProvider ?? throw new ArgumentNullException(nameof(iconUrlProvider));
        }

        // /api/v1/Packages
        [HttpGet]
        [HttpPost]
        [CacheOutput(NoCache = true)]
        public IHttpActionResult Get(ODataQueryOptions<V1FeedPackage> options)
        {
            if (!ODataQueryVerifier.AreODataOptionsAllowed(options, ODataQueryVerifier.V1Packages,
                _configurationService.Current.IsODataFilterEnabled, nameof(Get)))
            {
                return BadRequest(ODataQueryVerifier.GetValidationFailedMessage(options));
            }
            var queryable = _packagesRepository.GetAll()
                                .Where(p => !p.IsPrerelease && p.PackageStatusKey == PackageStatus.Available)
                                .Where(SemVerLevelKey.IsUnknownPredicate())
                                .WithoutSortOnColumn(Version)
                                .WithoutSortOnColumn(Id, ShouldIgnoreOrderById(options))
                                .ToV1FeedPackageQuery(_configurationService.GetSiteRoot(UseHttps()), _iconUrlProvider);

            return TrackedQueryResult(options, queryable, MaxPageSize, customQuery: true);
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

        // /api/v1/FindPackagesById()/$count?id=
        [HttpGet]
        [CacheOutput(ServerTimeSpan = NuGetODataConfig.GetByIdAndVersionCacheTimeInSeconds, Private = true, ClientTimeSpan = NuGetODataConfig.GetByIdAndVersionCacheTimeInSeconds)]
        public async Task<IHttpActionResult> FindPackagesByIdCount(ODataQueryOptions<V1FeedPackage> options, [FromODataUri]string id)
        {
            var result = await FindPackagesById(options, id);
            return result.FormattedAsCountResult<V1FeedPackage>();
        }

        private async Task<IHttpActionResult> GetCore(ODataQueryOptions<V1FeedPackage> options, string id, string version, bool return404NotFoundWhenNoResults)
        {
            var packages = _packagesRepository.GetAll()
                .Include(p => p.PackageRegistration)
                .Where(p => p.PackageRegistration.Id.Equals(id, StringComparison.OrdinalIgnoreCase)
                            && !p.IsPrerelease
                            && p.PackageStatusKey == PackageStatus.Available)
                .Where(SemVerLevelKey.IsUnknownPredicate());

            if (!string.IsNullOrEmpty(version))
            {
                packages = packages.Where(p => p.Version == version);
            }

            bool? customQuery = null;

            // try the search service
            try
            {
                var searchAdaptorResult = await SearchAdaptor.FindByIdAndVersionCore(
                    _searchService,
                    GetTraditionalHttpContext().Request,
                    packages,
                    id,
                    version,
                    semVerLevel: null);

                // If intercepted, create a paged queryresult
                if (searchAdaptorResult.ResultsAreProvidedBySearchService)
                {
                    customQuery = false;

                    // Packages provided by search service
                    packages = searchAdaptorResult.Packages;

                    // Add explicit Take() needed to limit search hijack result set size if $top is specified
                    var totalHits = packages.LongCount();

                    if (return404NotFoundWhenNoResults && totalHits == 0)
                    {
                        _telemetryService.TrackODataCustomQuery(customQuery);
                        return NotFound();
                    }

                    var pagedQueryable = packages
                        .Take(options.Top != null ? Math.Min(options.Top.Value, MaxPageSize) : MaxPageSize)
                        .ToV1FeedPackageQuery(GetSiteRoot(), _iconUrlProvider);

                    return TrackedQueryResult(
                        options,
                        pagedQueryable,
                        MaxPageSize,
                        totalHits,
                        (o, s, resultCount) => SearchAdaptor.GetNextLink(Request.RequestUri, resultCount, new { id }, o, s),
                        customQuery);
                }
                else
                {
                    customQuery = true;
                }
            }
            catch (Exception ex)
            {
                // Swallowing Exception intentionally. If *anything* goes wrong in search, just fall back to the database.
                // We don't want to break package restores. We do want to know if this happens, so here goes:
                QuietLog.LogHandledException(ex);
            }

            if (return404NotFoundWhenNoResults && !packages.Any())
            {
                _telemetryService.TrackODataCustomQuery(customQuery);
                return NotFound();
            }

            var queryable = packages.ToV1FeedPackageQuery(GetSiteRoot(), _iconUrlProvider);
            return TrackedQueryResult(options, queryable, MaxPageSize, customQuery);
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

            // Perform actual search
            var packages = _packagesRepository.GetAll()
                .Include(p => p.PackageRegistration)
                .Include(p => p.PackageRegistration.Owners)
                .Where(p => p.Listed && !p.IsPrerelease && p.PackageStatusKey == PackageStatus.Available)
                .Where(SemVerLevelKey.IsUnknownPredicate())
                .OrderBy(p => p.PackageRegistration.Id).ThenBy(p => p.Version)
                .AsNoTracking();

            // todo: search hijack should take queryOptions instead of manually parsing query options
            var searchAdaptorResult = await SearchAdaptor.SearchCore(
                _searchService,
                GetTraditionalHttpContext().Request,
                packages,
                searchTerm,
                targetFramework,
                includePrerelease: false, 
                semVerLevel: null);

            // Packages provided by search service (even when not hijacked)
            var query = searchAdaptorResult.Packages;
            bool? customQuery = null;

            // If intercepted, create a paged queryresult
            if (searchAdaptorResult.ResultsAreProvidedBySearchService)
            {
                customQuery = false;

                // Add explicit Take() needed to limit search hijack result set size if $top is specified
                var totalHits = query.LongCount();
                var pagedQueryable = query
                    .Take(options.Top != null ? Math.Min(options.Top.Value, MaxPageSize) : MaxPageSize)
                    .ToV1FeedPackageQuery(GetSiteRoot(), _iconUrlProvider);

                return TrackedQueryResult(
                    options,
                    pagedQueryable,
                    MaxPageSize,
                    totalHits,
                    (o, s, resultCount) => SearchAdaptor.GetNextLink(Request.RequestUri, resultCount, new { searchTerm, targetFramework }, o, s),
                    customQuery);
            }
            else
            {
                customQuery = true;
            }

            if (!ODataQueryVerifier.AreODataOptionsAllowed(options, ODataQueryVerifier.V1Search,
                _configurationService.Current.IsODataFilterEnabled, nameof(Search)))
            {
                return BadRequest(ODataQueryVerifier.GetValidationFailedMessage(options));
            }

            // If not, just let OData handle things
            var queryable = query.ToV1FeedPackageQuery(GetSiteRoot(), _iconUrlProvider);
            return TrackedQueryResult(options, queryable, MaxPageSize, customQuery);
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