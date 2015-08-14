// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.OData;
using System.Web.Http.OData.Query;
using NuGet;
using NuGetGallery.Configuration;
using NuGetGallery.OData;
using NuGetGallery.OData.QueryInterceptors;
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

        private readonly IEntityRepository<Package> _packagesRepository;
        private readonly ConfigurationService _configurationService;
        private readonly ISearchService _searchService;

        public ODataV2FeedController(
            IEntityRepository<Package> packagesRepository, 
            ConfigurationService configurationService,
            ISearchService searchService)
            : base(configurationService)
        {
            _packagesRepository = packagesRepository;
            _configurationService = configurationService;
            _searchService = searchService;
        }

        // /api/v2/Packages
        [HttpGet]
        [HttpPost]
        [CacheOutput(NoCache = true)]
        public IHttpActionResult Get(ODataQueryOptions<V2FeedPackage> options)
        {
            var queryable = _packagesRepository
                .GetAll()
                .UseSearchService(_searchService, null, _configurationService.GetSiteRoot(UseHttps()), _configurationService.Features.FriendlyLicenses)
                .WithoutVersionSort()
                .ToV2FeedPackageQuery(_configurationService.GetSiteRoot(UseHttps()), _configurationService.Features.FriendlyLicenses)
                .InterceptWith(new NormalizeVersionInterceptor());

            return QueryResult(options, queryable, MaxPageSize);
        }

        // /api/v2/Packages/$count
        [HttpGet]
        [CacheOutput(NoCache = true)]
        public IHttpActionResult GetCount(ODataQueryOptions<V2FeedPackage> options)
        {
            return Get(options).FormattedAsCountResult<V2FeedPackage>();
        }

        // /api/v2/Packages(Id=,Version=)
        [HttpGet]
        [CacheOutput(ServerTimeSpan = NuGetODataConfig.GetByIdAndVersionCacheTimeInSeconds, Private = true, ClientTimeSpan = NuGetODataConfig.GetByIdAndVersionCacheTimeInSeconds)]
        public async Task<IHttpActionResult> Get(ODataQueryOptions<V2FeedPackage> options, string id, string version)
        {
            var result = await GetCore(options, id, version);
            return result.FormattedAsSingleResult<V2FeedPackage>();
        }

        // /api/v2/FindPackagesById()?id=
        [HttpGet]
        [HttpPost]
        [CacheOutput(ServerTimeSpan = NuGetODataConfig.GetByIdAndVersionCacheTimeInSeconds, Private = true, ClientTimeSpan = NuGetODataConfig.GetByIdAndVersionCacheTimeInSeconds)]
        public async Task<IHttpActionResult> FindPackagesById(ODataQueryOptions<V2FeedPackage> options, [FromODataUri]string id)
        {
            return await GetCore(options, id, null);
        }

        private async Task<IHttpActionResult> GetCore(ODataQueryOptions<V2FeedPackage> options, string id, string version)
        {
            var packages = _packagesRepository.GetAll()
                .Include(p => p.PackageRegistration)
                .Where(p => p.PackageRegistration.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

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

            var queryable = packages.ToV2FeedPackageQuery(GetSiteRoot(), _configurationService.Features.FriendlyLicenses);
            return QueryResult(options, queryable, MaxPageSize);
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
        [CacheOutput(ServerTimeSpan = NuGetODataConfig.SearchCacheTimeInSeconds, ClientTimeSpan = NuGetODataConfig.SearchCacheTimeInSeconds)]
        public async Task<IHttpActionResult> Search(
            ODataQueryOptions<V2FeedPackage> options, 
            [FromODataUri]string searchTerm = "", 
            [FromODataUri]string targetFramework = "",
            [FromODataUri]bool includePrerelease = false)
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
                .Where(p => p.Listed);

            // todo: search hijack should take options instead of manually parsing query options
            var query = await SearchAdaptor.SearchCore(
                _searchService, GetTraditionalHttpContext().Request, packages, searchTerm, targetFramework, includePrerelease, curatedFeed: null);

            // Build queryable (explicit Take() needed to limit search hijack result set size if $top is specified)
            var totalHits = query.LongCount();
            var queryable = query
                .Take(options.Top != null ? Math.Min(options.Top.Value, MaxPageSize) : MaxPageSize)
                .ToV2FeedPackageQuery(GetSiteRoot(), _configurationService.Features.FriendlyLicenses);

            return QueryResult(options, queryable, MaxPageSize, totalHits, (o, s) => 
                SearchAdaptor.GetNextLink(Request.RequestUri, queryable, new { searchTerm, targetFramework, includePrerelease }, o, s, false));
        }

        // /api/v2/Search()/$count?searchTerm=&targetFramework=&includePrerelease=
        [HttpGet]
        [CacheOutput(ServerTimeSpan = NuGetODataConfig.SearchCacheTimeInSeconds, ClientTimeSpan = NuGetODataConfig.SearchCacheTimeInSeconds)]
        public async Task<IHttpActionResult> SearchCount(
            ODataQueryOptions<V2FeedPackage> options, 
            [FromODataUri]string searchTerm = "", 
            [FromODataUri]string targetFramework = "",
            [FromODataUri]bool includePrerelease = false)
        {
            var searchResults = await Search(options, searchTerm, targetFramework, includePrerelease);
            return searchResults.FormattedAsCountResult<V2FeedPackage>();
        }

        // /api/v2/GetUpdates()?packageIds=&versions=&includePrerelease=&includeAllVersions=&targetFrameworks=&versionConstraints=
        [HttpGet]
        [HttpPost]
        public IHttpActionResult GetUpdates(
            ODataQueryOptions<V2FeedPackage> options,
            [FromODataUri]string packageIds,
            [FromODataUri]string versions,
            [FromODataUri]bool includePrerelease, 
            [FromODataUri]bool includeAllVersions, 
            [FromODataUri]string targetFrameworks = "", 
            [FromODataUri]string versionConstraints = "")
        {
            if (string.IsNullOrEmpty(packageIds) || string.IsNullOrEmpty(versions))
            {
                return Ok(Enumerable.Empty<V2FeedPackage>().AsQueryable());
            }

            // Workaround https://github.com/NuGet/NuGetGallery/issues/674 for NuGet 2.1 client.
            // Can probably eventually be retired (when nobody uses 2.1 anymore...)
            // Note - it was URI un-escaping converting + to ' ', undoing that is actually a pretty conservative substitution because
            // space characters are never acepted as valid by VersionUtility.ParseFrameworkName.
            if (!string.IsNullOrEmpty(targetFrameworks))
            {
                targetFrameworks = targetFrameworks.Replace(' ', '+');
            }

            var idValues = packageIds.Trim().ToLowerInvariant().Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
            var versionValues = versions.Trim().Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
            var targetFrameworkValues = string.IsNullOrEmpty(targetFrameworks)
                                            ? null
                                            : targetFrameworks.Split('|').Select(VersionUtility.ParseFrameworkName).ToList();
            var versionConstraintValues = string.IsNullOrEmpty(versionConstraints)
                                            ? new string[idValues.Length]
                                            : versionConstraints.Split('|');

            if (idValues.Length == 0 || idValues.Length != versionValues.Length || idValues.Length != versionConstraintValues.Length)
            {
                // Exit early if the request looks invalid
                return Ok(Enumerable.Empty<V2FeedPackage>().AsQueryable());
            }

            var versionLookup = idValues.Select((id, i) =>
            {
                SemanticVersion currentVersion;
                if (SemanticVersion.TryParse(versionValues[i], out currentVersion))
                {
                    IVersionSpec versionConstraint = null;
                    if (versionConstraintValues[i] != null)
                    {
                        if (!VersionUtility.TryParseVersionSpec(versionConstraintValues[i], out versionConstraint))
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

            var packages = _packagesRepository.GetAll()
                .Include(p => p.PackageRegistration)
                .Include(p => p.SupportedFrameworks)
                .Where(p =>
                    p.Listed && (includePrerelease || !p.IsPrerelease) &&
                    idValues.Contains(p.PackageRegistration.Id.ToLower()))
                .OrderBy(p => p.PackageRegistration.Id);

            var queryable = GetUpdates(packages, versionLookup, targetFrameworkValues, includeAllVersions)
                .AsQueryable()
                .ToV2FeedPackageQuery(GetSiteRoot(), _configurationService.Features.FriendlyLicenses);

            return QueryResult(options, queryable, MaxPageSize);
        }

        // /api/v2/GetUpdates()/$count?packageIds=&versions=&includePrerelease=&includeAllVersions=&targetFrameworks=&versionConstraints=
        [HttpGet]
        [HttpPost]
        public IHttpActionResult GetUpdatesCount(
            ODataQueryOptions<V2FeedPackage> options,
            [FromODataUri]string packageIds,
            [FromODataUri]string versions,
            [FromODataUri]bool includePrerelease,
            [FromODataUri]bool includeAllVersions,
            [FromODataUri]string targetFrameworks = "",
            [FromODataUri]string versionConstraints = "")
        {
            return GetUpdates(options, packageIds, versions, includePrerelease, includeAllVersions, targetFrameworks, versionConstraints)
                .FormattedAsCountResult<V2FeedPackage>();
        }

        private static IEnumerable<Package> GetUpdates(
            IEnumerable<Package> packages,
            ILookup<string, Tuple<SemanticVersion, IVersionSpec>> versionLookup,
            IEnumerable<FrameworkName> targetFrameworkValues,
            bool includeAllVersions)
        {
            var updates = from p in packages.AsEnumerable()
                          let version = SemanticVersion.Parse(p.Version)
                          where versionLookup[p.PackageRegistration.Id]
                            .Any(versionTuple =>
                            {
                                SemanticVersion clientVersion = versionTuple.Item1;
                                var supportedPackageFrameworks = p.SupportedFrameworks.Select(f => f.FrameworkName);

                                IVersionSpec versionConstraint = versionTuple.Item2;

                                return (version > clientVersion) &&
                                        (targetFrameworkValues == null ||
                                        targetFrameworkValues.Any(s => VersionUtility.IsCompatible(s, supportedPackageFrameworks))) &&
                                        (versionConstraint == null || versionConstraint.Satisfies(version));
                            })
                          orderby p.PackageRegistration.Id, SemanticVersion.Parse(p.Version) descending 
                          select p;

            if (!includeAllVersions)
            {
                updates = updates.GroupBy(p => p.PackageRegistration.Id)
                    .Select(g => g.OrderByDescending(p => SemanticVersion.Parse(p.Version)).First());
            }
            return updates;
        }
    }
}