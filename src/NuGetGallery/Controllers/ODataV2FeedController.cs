// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.OData;
using System.Web.Http.OData.Query;
using NuGet.Frameworks;
using NuGet.Services.Entities;
using NuGet.Versioning;
using NuGetGallery.Configuration;
using NuGetGallery.Infrastructure.Search;
using NuGetGallery.OData;
using NuGetGallery.OData.QueryFilter;
using NuGetGallery.OData.QueryInterceptors;
using NuGetGallery.Services;
using NuGetGallery.WebApi;
using QueryInterceptor;
using WebApi.OutputCache.V2;

// ReSharper disable once CheckNamespace
namespace NuGetGallery.Controllers
{
    public class ODataV2FeedController
        : NuGetODataController
    {
        private const int MaxPageSize = SearchAdaptor.MaxPageSize;

        private readonly IReadOnlyEntityRepository<Package> _packagesRepository;
        private readonly IEntityRepository<Package> _readWritePackagesRepository;
        private readonly IGalleryConfigurationService _configurationService;
        private readonly IHijackSearchServiceFactory _searchServiceFactory;
        private readonly IFeatureFlagService _featureFlagService;

        public ODataV2FeedController(
            IReadOnlyEntityRepository<Package> packagesRepository,
            IEntityRepository<Package> readWritePackagesRepository,
            IGalleryConfigurationService configurationService,
            IHijackSearchServiceFactory searchServiceFactory,
            ITelemetryService telemetryService,
            IFeatureFlagService featureFlagService)
            : base(configurationService, telemetryService)
        {
            _packagesRepository = packagesRepository ?? throw new ArgumentNullException(nameof(packagesRepository));
            _readWritePackagesRepository = readWritePackagesRepository ?? throw new ArgumentNullException(nameof(readWritePackagesRepository));
            _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
            _searchServiceFactory = searchServiceFactory ?? throw new ArgumentNullException(nameof(searchServiceFactory));
            _featureFlagService = featureFlagService ?? throw new ArgumentNullException(nameof(featureFlagService));
        }

        // /api/v2/Packages?semVerLevel=
        [HttpGet]
        [HttpPost]
        [CacheOutput(NoCache = true)]
        public async Task<IHttpActionResult> Get(
            ODataQueryOptions<V2FeedPackage> options,
            [FromUri]string semVerLevel = null)
        {
            // Setup the search
            var packages = GetAll()
                            .Where(p => p.PackageStatusKey == PackageStatus.Available)
                            .Where(SemVerLevelKey.IsPackageCompliantWithSemVerLevelPredicate(semVerLevel))
                            .WithoutSortOnColumn(Version)
                            .WithoutSortOnColumn(Id, ShouldIgnoreOrderById(options))
                            .InterceptWith(new NormalizeVersionInterceptor());

            var semVerLevelKey = SemVerLevelKey.ForSemVerLevel(semVerLevel);
            bool? customQuery = null;

            // Try the search service
            try
            {
                var searchService = _searchServiceFactory.GetService();
                HijackableQueryParameters hijackableQueryParameters = null;
                if (searchService is ExternalSearchService && SearchHijacker.IsHijackable(options, out hijackableQueryParameters))
                {
                    var searchAdaptorResult = await SearchAdaptor.FindByIdAndVersionCore(
                        searchService,
                        GetTraditionalHttpContext().Request, 
                        packages,
                        hijackableQueryParameters.Id, 
                        hijackableQueryParameters.Version,
                        semVerLevel: semVerLevel);

                    // If intercepted, create a paged queryresult
                    if (searchAdaptorResult.ResultsAreProvidedBySearchService)
                    {
                        customQuery = false;

                        // Packages provided by search service
                        packages = searchAdaptorResult.Packages;

                        // Add explicit Take() needed to limit search hijack result set size if $top is specified
                        var totalHits = packages.LongCount();
                        var pagedQueryable = packages
                            .Take(options.Top != null ? Math.Min(options.Top.Value, MaxPageSize) : MaxPageSize)
                            .ToV2FeedPackageQuery(
                                GetSiteRoot(),
                                _configurationService.Features.FriendlyLicenses,
                                semVerLevelKey);

                        return TrackedQueryResult(
                            options,
                            pagedQueryable,
                            MaxPageSize,
                            totalHits,
                            (o, s, resultCount) => SearchAdaptor.GetNextLink(Request.RequestUri, resultCount, null, o, s, semVerLevelKey),
                            customQuery);
                    }
                    else
                    {
                        customQuery = true;
                    }
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

            // Reject only when try to reach database.
            if (!ODataQueryVerifier.AreODataOptionsAllowed(options, ODataQueryVerifier.V2Packages,
                _configurationService.Current.IsODataFilterEnabled, nameof(Get)))
            {
                return BadRequest(ODataQueryVerifier.GetValidationFailedMessage(options));
            }

            var queryable = packages.ToV2FeedPackageQuery(
                GetSiteRoot(), 
                _configurationService.Features.FriendlyLicenses, 
                semVerLevelKey);

            return TrackedQueryResult(options, queryable, MaxPageSize, customQuery);
        }

        // /api/v2/Packages/$count?semVerLevel=
        [HttpGet]
        [CacheOutput(NoCache = true)]
        public async Task<IHttpActionResult> GetCount(
            ODataQueryOptions<V2FeedPackage> options,
            [FromUri]string semVerLevel = null)
        {
            return (await Get(options, semVerLevel)).FormattedAsCountResult<V2FeedPackage>();
        }

        // /api/v2/Packages(Id=,Version=)
        [HttpGet]
        [ODataCacheOutput(
            ODataCachedEndpoint.GetSpecificPackage,
            serverTimeSpan: ODataCacheConfiguration.DefaultGetByIdAndVersionCacheTimeInSeconds,
            Private = true,
            ClientTimeSpan = ODataCacheConfiguration.DefaultGetByIdAndVersionCacheTimeInSeconds)]
        public async Task<IHttpActionResult> Get(
            ODataQueryOptions<V2FeedPackage> options, 
            string id, 
            string version)
        {
            // We are defaulting to semVerLevel = "2.0.0" by design.
            // The client is requesting a specific package version and should support what it requests.
            // If not, too bad :)
            var result = await GetCore(
                options, 
                id, 
                version, 
                semVerLevel: SemVerLevelKey.SemVerLevel2, 
                return404NotFoundWhenNoResults: true);

            return result.FormattedAsSingleResult<V2FeedPackage>();
        }

        // /api/v2/FindPackagesById()?id=&semVerLevel=
        [HttpGet]
        [HttpPost]
        [ODataCacheOutput(
            ODataCachedEndpoint.FindPackagesById,
            serverTimeSpan: ODataCacheConfiguration.DefaultGetByIdAndVersionCacheTimeInSeconds,
            Private = true,
            ClientTimeSpan = ODataCacheConfiguration.DefaultGetByIdAndVersionCacheTimeInSeconds)]
        public async Task<IHttpActionResult> FindPackagesById(
            ODataQueryOptions<V2FeedPackage> options, 
            [FromODataUri]string id,
            [FromUri]string semVerLevel = null)
        {
            if (string.IsNullOrEmpty(id))
            {
                var semVerLevelKey = SemVerLevelKey.ForSemVerLevel(semVerLevel);

                var emptyResult = Enumerable.Empty<Package>().AsQueryable()
                    .ToV2FeedPackageQuery(
                        GetSiteRoot(), 
                        _configurationService.Features.FriendlyLicenses, 
                        semVerLevelKey);

                return TrackedQueryResult(options, emptyResult, MaxPageSize, customQuery: false);
            }

            return await GetCore(
                options, 
                id, 
                version: null, 
                semVerLevel: semVerLevel, 
                return404NotFoundWhenNoResults: false);
        }

        // /api/v2/FindPackagesById()/$count?semVerLevel=
        [HttpGet]
        [ODataCacheOutput(
            ODataCachedEndpoint.FindPackagesByIdCount,
            serverTimeSpan: ODataCacheConfiguration.DefaultFindPackagesByIdCountCacheTimeInSeconds,
            NoCache = true)]
        public async Task<IHttpActionResult> FindPackagesByIdCount(
            ODataQueryOptions<V2FeedPackage> options,
            [FromODataUri]string id,
            [FromUri]string semVerLevel = null)
        {
            return (await FindPackagesById(options, id, semVerLevel)).FormattedAsCountResult<V2FeedPackage>();
        }

        private async Task<IHttpActionResult> GetCore(
            ODataQueryOptions<V2FeedPackage> options, 
            string id, 
            string version, 
            string semVerLevel,
            bool return404NotFoundWhenNoResults)
        {
            var packages = GetAll()
                .Include(p => p.PackageRegistration)
                .Where(p => p.PackageStatusKey == PackageStatus.Available
                            && p.PackageRegistration.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
                .Where(SemVerLevelKey.IsPackageCompliantWithSemVerLevelPredicate(semVerLevel));

            if (!string.IsNullOrEmpty(version))
            {
                NuGetVersion nugetVersion;
                if (NuGetVersion.TryParse(version, out nugetVersion))
                {
                    // Our APIs expect to receive normalized version strings.
                    // We need to compare normalized versions or we can never retrieve SemVer2 package versions.
                    var normalizedString = nugetVersion.ToNormalizedString();
                    packages = packages.Where(p => p.NormalizedVersion == normalizedString);
                }
            }

            var semVerLevelKey = SemVerLevelKey.ForSemVerLevel(semVerLevel);
            bool? customQuery = null;

            // try the search service
            try
            {
                var searchService = _searchServiceFactory.GetService();
                var searchAdaptorResult = await SearchAdaptor.FindByIdAndVersionCore(
                    searchService,
                    GetTraditionalHttpContext().Request, 
                    packages, 
                    id, 
                    version, 
                    semVerLevel: semVerLevel);

                // If intercepted, create a paged queryresult
                if (searchAdaptorResult.ResultsAreProvidedBySearchService)
                {
                    customQuery = false;

                    // Packages provided by search service
                    packages = searchAdaptorResult.Packages;

                    // Add explicit Take() needed to limit search hijack result set size if $top is specified
                    var totalHits = packages.LongCount();

                    if (totalHits == 0 && return404NotFoundWhenNoResults)
                    {
                        _telemetryService.TrackODataCustomQuery(customQuery);
                        return NotFound();
                    }

                    var pagedQueryable = packages
                        .Take(options.Top != null ? Math.Min(options.Top.Value, MaxPageSize) : MaxPageSize)
                        .ToV2FeedPackageQuery(
                            GetSiteRoot(), 
                            _configurationService.Features.FriendlyLicenses, 
                            semVerLevelKey);

                    return TrackedQueryResult(
                        options,
                        pagedQueryable,
                        MaxPageSize,
                        totalHits,
                        (o, s, resultCount) => SearchAdaptor.GetNextLink(Request.RequestUri, resultCount, new { id }, o, s, semVerLevelKey),
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

            var queryable = packages.ToV2FeedPackageQuery(
                GetSiteRoot(), 
                _configurationService.Features.FriendlyLicenses, 
                semVerLevelKey);

            return TrackedQueryResult(options, queryable, MaxPageSize, customQuery);
        }

        // /api/v2/Packages(Id=,Version=)/propertyName
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

        // /api/v2/Search()?searchTerm=&targetFramework=&includePrerelease=
        [HttpGet]
        [HttpPost]
        [ODataCacheOutput(
            ODataCachedEndpoint.Search,
            serverTimeSpan: ODataCacheConfiguration.DefaultSearchCacheTimeInSeconds,
            ClientTimeSpan = ODataCacheConfiguration.DefaultSearchCacheTimeInSeconds)]
        public async Task<IHttpActionResult> Search(
            ODataQueryOptions<V2FeedPackage> options,
            [FromODataUri]string searchTerm = "",
            [FromODataUri]string targetFramework = "",
            [FromODataUri]bool includePrerelease = false,
            [FromUri]string semVerLevel = null)
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
            var packages = GetAll()
                .Include(p => p.PackageRegistration)
                .Include(p => p.PackageRegistration.Owners)
                .Where(p => p.Listed && p.PackageStatusKey == PackageStatus.Available)
                .Where(SemVerLevelKey.IsPackageCompliantWithSemVerLevelPredicate(semVerLevel))
                .OrderBy(p => p.PackageRegistration.Id).ThenBy(p => p.Version)
                .AsNoTracking();

            // todo: search hijack should take options instead of manually parsing query options
            var searchService = _searchServiceFactory.GetService();
            var searchAdaptorResult = await SearchAdaptor.SearchCore(
                searchService,
                GetTraditionalHttpContext().Request, 
                packages, 
                searchTerm, 
                targetFramework, 
                includePrerelease, 
                semVerLevel: semVerLevel);

            // Packages provided by search service (even when not hijacked)
            var query = searchAdaptorResult.Packages;

            var semVerLevelKey = SemVerLevelKey.ForSemVerLevel(semVerLevel);
            bool? customQuery = null;

            // If intercepted, create a paged queryresult
            if (searchAdaptorResult.ResultsAreProvidedBySearchService)
            {
                customQuery = false;

                // Add explicit Take() needed to limit search hijack result set size if $top is specified
                var totalHits = query.LongCount();
                var pagedQueryable = query
                    .Take(options.Top != null ? Math.Min(options.Top.Value, MaxPageSize) : MaxPageSize)
                    .ToV2FeedPackageQuery(
                        GetSiteRoot(), 
                        _configurationService.Features.FriendlyLicenses, 
                        semVerLevelKey);

                return TrackedQueryResult(
                    options,
                    pagedQueryable,
                    MaxPageSize,
                    totalHits,
                    (o, s, resultCount) =>
                        {
                            // The nuget.exe 2.x list command does not like the next link at the bottom when a $top is passed.
                            // Strip it of for backward compatibility.
                            if (o.Top == null || (resultCount.HasValue && o.Top.Value >= resultCount.Value))
                            {
                                return SearchAdaptor.GetNextLink(Request.RequestUri, resultCount, new { searchTerm, targetFramework, includePrerelease }, o, s, semVerLevelKey);
                            }
                            return null;
                        },
                    customQuery);
            }
            else
            {
                customQuery = true;
            }

            //Reject only when try to reach database.
            if (!ODataQueryVerifier.AreODataOptionsAllowed(options, ODataQueryVerifier.V2Search,
                _configurationService.Current.IsODataFilterEnabled, nameof(Search)))
            {
                return BadRequest(ODataQueryVerifier.GetValidationFailedMessage(options));
            }

            // If not, just let OData handle things
            var queryable = query.ToV2FeedPackageQuery(
                GetSiteRoot(), 
                _configurationService.Features.FriendlyLicenses, 
                semVerLevelKey);

            return TrackedQueryResult(options, queryable, MaxPageSize, customQuery);
        }

        // /api/v2/Search()/$count?searchTerm=&targetFramework=&includePrerelease=&semVerLevel=
        [HttpGet]
        [ODataCacheOutput(
            ODataCachedEndpoint.Search,
            serverTimeSpan: ODataCacheConfiguration.DefaultSearchCacheTimeInSeconds,
            ClientTimeSpan = ODataCacheConfiguration.DefaultSearchCacheTimeInSeconds)]
        public async Task<IHttpActionResult> SearchCount(
            ODataQueryOptions<V2FeedPackage> options,
            [FromODataUri]string searchTerm = "",
            [FromODataUri]string targetFramework = "",
            [FromODataUri]bool includePrerelease = false,
            [FromUri]string semVerLevel = null)
        {
            var searchResults = await Search(
                options, 
                searchTerm, 
                targetFramework, 
                includePrerelease, 
                semVerLevel);
            return searchResults.FormattedAsCountResult<V2FeedPackage>();
        }

        // /api/v2/GetUpdates()?packageIds=&versions=&includePrerelease=&includeAllVersions=&targetFrameworks=&versionConstraints=&semVerLevel=
        [HttpGet]
        [HttpPost]
        public IHttpActionResult GetUpdates(
            ODataQueryOptions<V2FeedPackage> options,
            [FromODataUri]string packageIds,
            [FromODataUri]string versions,
            [FromODataUri]bool includePrerelease,
            [FromODataUri]bool includeAllVersions,
            [FromODataUri]string targetFrameworks = "",
            [FromODataUri]string versionConstraints = "",
            [FromUri]string semVerLevel = null)
        {
            if (string.IsNullOrEmpty(packageIds) || string.IsNullOrEmpty(versions))
            {
                return TrackedQueryResult(
                    options,
                    Enumerable.Empty<V2FeedPackage>().AsQueryable(),
                    MaxPageSize,
                    customQuery: false);
            }

            if (!ODataQueryVerifier.AreODataOptionsAllowed(options, ODataQueryVerifier.V2GetUpdates,
                _configurationService.Current.IsODataFilterEnabled, nameof(GetUpdates)))
            {
                return BadRequest(ODataQueryVerifier.GetValidationFailedMessage(options));
            }

            // Workaround https://github.com/NuGet/NuGetGallery/issues/674 for NuGet 2.1 client.
            // Can probably eventually be retired (when nobody uses 2.1 anymore...)
            // Note - it was URI un-escaping converting + to ' ', undoing that is actually a pretty conservative substitution because
            // space characters are never accepted as valid by VersionUtility.ParseFrameworkName.
            if (!string.IsNullOrEmpty(targetFrameworks))
            {
                targetFrameworks = targetFrameworks.Replace(' ', '+');
            }

            var idValues = packageIds.Trim().ToLowerInvariant().Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
            var versionValues = versions.Trim().Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
            var targetFrameworkValues = string.IsNullOrEmpty(targetFrameworks)
                                            ? null
                                            : targetFrameworks.Split('|').Select(tfx => NuGetFramework.Parse(tfx)).ToList();
            var versionConstraintValues = string.IsNullOrEmpty(versionConstraints)
                                            ? new string[idValues.Length]
                                            : versionConstraints.Split('|');

            if (idValues.Length == 0 || idValues.Length != versionValues.Length || idValues.Length != versionConstraintValues.Length)
            {
                // Exit early if the request looks invalid
                return TrackedQueryResult(
                    options,
                    Enumerable.Empty<V2FeedPackage>().AsQueryable(),
                    MaxPageSize,
                    customQuery: false);
            }

            var versionLookup = idValues.Select((id, i) =>
            {
                NuGetVersion currentVersion;
                if (NuGetVersion.TryParse(versionValues[i], out currentVersion))
                {
                    VersionRange versionConstraint = null;
                    if (versionConstraintValues[i] != null)
                    {
                        if (!VersionRange.TryParse(versionConstraintValues[i], out versionConstraint))
                        {
                            versionConstraint = null;
                        }
                    }
                    return Tuple.Create(id, Tuple.Create(currentVersion, versionConstraint));
                }
                return null;
            })
                .Where(t => t != null)
                .ToLookup(t => t.Item1, t => t.Item2, StringComparer.OrdinalIgnoreCase);

            var packages = GetAll()
                .Include(p => p.PackageRegistration)
                .Include(p => p.SupportedFrameworks)
                .Where(p => p.Listed && (includePrerelease || !p.IsPrerelease)
                            && idValues.Contains(p.PackageRegistration.Id.ToLower())
                            && p.PackageStatusKey == PackageStatus.Available)
                .OrderBy(p => p.PackageRegistration.Id);

            var semVerLevelKey = SemVerLevelKey.ForSemVerLevel(semVerLevel);
            var queryable = GetUpdates(packages, versionLookup, targetFrameworkValues, includeAllVersions, semVerLevel)
                .AsQueryable()
                .ToV2FeedPackageQuery(
                    GetSiteRoot(), 
                    _configurationService.Features.FriendlyLicenses, 
                    semVerLevelKey);

            return TrackedQueryResult(options, queryable, MaxPageSize, customQuery: false);
        }

        // /api/v2/GetUpdates()/$count?packageIds=&versions=&includePrerelease=&includeAllVersions=&targetFrameworks=&versionConstraints=&semVerLevel=
        [HttpGet]
        [HttpPost]
        public IHttpActionResult GetUpdatesCount(
            ODataQueryOptions<V2FeedPackage> options,
            [FromODataUri]string packageIds,
            [FromODataUri]string versions,
            [FromODataUri]bool includePrerelease,
            [FromODataUri]bool includeAllVersions,
            [FromODataUri]string targetFrameworks = "",
            [FromODataUri]string versionConstraints = "",
            [FromUri]string semVerLevel = null)
        {
            return GetUpdates(
                options, 
                packageIds, 
                versions, 
                includePrerelease, 
                includeAllVersions, 
                targetFrameworks, 
                versionConstraints, 
                semVerLevel)
                .FormattedAsCountResult<V2FeedPackage>();
        }

        private static IEnumerable<Package> GetUpdates(
            IEnumerable<Package> packages,
            ILookup<string, Tuple<NuGetVersion, VersionRange>> versionLookup,
            IEnumerable<NuGetFramework> targetFrameworkValues,
            bool includeAllVersions,
            string semVerLevel)
        {
            var isSemVerLevelCompliant = SemVerLevelKey.IsPackageCompliantWithSemVerLevelPredicate(semVerLevel).Compile();

            var updates = from p in packages.AsEnumerable()
                          let version = NuGetVersion.Parse(p.Version)
                          where isSemVerLevelCompliant(p)
                                && versionLookup[p.PackageRegistration.Id].Any(versionTuple =>
                                {
                                    NuGetVersion clientVersion = versionTuple.Item1;
                                    var supportedPackageFrameworks = p.SupportedFrameworks.Select(f => f.FrameworkName);

                                    VersionRange versionConstraint = versionTuple.Item2;

                                    return version > clientVersion 
                                            && (targetFrameworkValues == null 
                                                || !supportedPackageFrameworks.Any() 
                                                || targetFrameworkValues.Any(s => supportedPackageFrameworks.Any(supported => NuGetFrameworkUtility.IsCompatibleWithFallbackCheck(s, supported)))) 
                                            && (versionConstraint == null 
                                                || versionConstraint.Satisfies(version));
                                })
                          select p;

            if (!includeAllVersions)
            {
                updates = updates.GroupBy(p => p.PackageRegistration.Id)
                    .Select(g => g.OrderByDescending(p => NuGetVersion.Parse(p.Version)).First());
            }

            return updates;
        }

        internal IQueryable<Package> GetAll()
        {
            if (_featureFlagService.IsODataDatabaseReadOnlyEnabled())
            {
                return _packagesRepository.GetAll();
            }
            return _readWritePackagesRepository.GetAll();
        }
    }
}