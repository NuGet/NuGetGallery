// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net.Http;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.OData;
using System.Web.Http.OData.Query;
using NuGet;
using NuGetGallery.Configuration;
using NuGetGallery.OData;
using NuGetGallery.OData.QueryInterceptors;
using QueryInterceptor;
using WebApi.OutputCache.V2;

// ReSharper disable once CheckNamespace
namespace NuGetGallery.Controllers
{
    public class ODataV2FeedController 
        : NuGetODataController
    {
        private readonly IEntityRepository<Package> _packagesRepository;
        private readonly ConfigurationService _configurationService;
        private readonly ISearchService _searchService;

        private static readonly ODataQuerySettings SearchQuerySettings = new ODataQuerySettings 
        { 
             HandleNullPropagation = HandleNullPropagationOption.False,
             EnsureStableOrdering = true
        };

        public ODataV2FeedController(IEntityRepository<Package> packagesRepository, ConfigurationService configurationService, ISearchService searchService)
            : base(configurationService)
        {
            _packagesRepository = packagesRepository;
            _configurationService = configurationService;
            _searchService = searchService;
        }

        [HttpGet, HttpPost, EnableQuery(PageSize = SearchAdaptor.MaxPageSize, HandleNullPropagation = HandleNullPropagationOption.False, EnsureStableOrdering = true)]
        public IQueryable<V2FeedPackage> Get()
        {
            var packages = _packagesRepository
                .GetAll()
                .UseSearchService(_searchService, null, _configurationService.GetSiteRoot(UseHttps()), _configurationService.Features.FriendlyLicenses)
                .WithoutVersionSort()
                .ToV2FeedPackageQuery(_configurationService.GetSiteRoot(UseHttps()), _configurationService.Features.FriendlyLicenses)
                .InterceptWith(new NormalizeVersionInterceptor());

            return packages;
        }

        [HttpGet]
        public HttpResponseMessage GetCount(ODataQueryOptions<V2FeedPackage> options)
        {
            var queryResults = (IQueryable<V2FeedPackage>)options.ApplyTo(Get());
            var count = queryResults.Count();

            return CountResult(count);
        }

        [HttpGet, EnableQuery(PageSize = SearchAdaptor.MaxPageSize, HandleNullPropagation = HandleNullPropagationOption.False, EnsureStableOrdering = true)]
        public async Task<IHttpActionResult> Get(string id, string version)
        {
            return await GetCore(id, version);
        }

        [HttpGet, HttpPost, EnableQuery(PageSize = SearchAdaptor.MaxPageSize, HandleNullPropagation = HandleNullPropagationOption.False, EnsureStableOrdering = true)]
        public async Task<IHttpActionResult> FindPackagesById([FromODataUri]string id)
        {
            return await GetCore(id, null);
        }

        private async Task<IHttpActionResult> GetCore(string id, string version)
        {
            // todo: route through search service?

            if (string.IsNullOrWhiteSpace(id))
            {
                return BadRequest("Parameter 'id' must be specified.");
            }

            var packages = _packagesRepository.GetAll()
                .Include(p => p.PackageRegistration)
                .Where(p => p.PackageRegistration.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrEmpty(version))
            {
                packages = packages.Where(p => p.Version == version); // todo: normalizedversion?
            }

            if (_configurationService.Features.PackageRestoreViaSearch)
            {
                try
                {
                    packages = await SearchAdaptor.FindByIdCore(_searchService, GetTraditionalHttpContext().Request, packages, id, curatedFeed: null);
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

        [HttpGet, HttpPost, CacheOutput(ServerTimeSpan = NuGetODataConfig.SearchCacheTime)]
        public async Task<IEnumerable<V2FeedPackage>> Search(ODataQueryOptions<V2FeedPackage> queryOptions, [FromODataUri] string searchTerm = "", [FromODataUri] string targetFramework = "", [FromODataUri] bool includePrerelease = false)
        {
            // Ensure we can provide paging
            var pageSize = queryOptions.Top != null ? (int?)null : SearchAdaptor.MaxPageSize;
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
            var packages = _packagesRepository.GetAll()
                .Include(p => p.PackageRegistration)
                .Include(p => p.PackageRegistration.Owners)
                .Where(p => p.Listed);

            // todo: search hijack should take queryOptions instead of manually parsing query options
            var query = await SearchAdaptor.SearchCore(_searchService, GetTraditionalHttpContext().Request, packages, searchTerm, targetFramework, includePrerelease, curatedFeed: null);

            var totalHits = query.LongCount();
            var convertedQuery = query
                .ToV2FeedPackageQuery(GetSiteRoot(), _configurationService.Features.FriendlyLicenses);

            // apply OData query options + limit total of entries explicitly
            convertedQuery = (IQueryable<V2FeedPackage>)queryOptions.ApplyTo(
                convertedQuery.Take(pageSize ?? SearchAdaptor.MaxPageSize)); 

            var nextLink = SearchAdaptor.GetNextLink(Request.RequestUri, convertedQuery, new { searchTerm, targetFramework, includePrerelease }, queryOptions, settings, false);

            return new PageResult<V2FeedPackage>(convertedQuery, nextLink, totalHits);
        }

        [HttpGet, CacheOutput(ServerTimeSpan = NuGetODataConfig.SearchCacheTime)]
        public async Task<HttpResponseMessage> SearchCount(ODataQueryOptions<V2FeedPackage> queryOptions, [FromODataUri] string searchTerm = "", [FromODataUri] string targetFramework = "", [FromODataUri] bool includePrerelease = false)
        {
            var queryResults = await Search(queryOptions, searchTerm, targetFramework, includePrerelease);

            var pageResult = queryResults as PageResult;
            if (pageResult != null && pageResult.Count.HasValue)
            {
                return CountResult(pageResult.Count.Value);
            }

            return CountResult(queryResults.LongCount());
        }

        [HttpGet, HttpPost, EnableQuery(PageSize = SearchAdaptor.MaxPageSize, HandleNullPropagation = HandleNullPropagationOption.False, EnsureStableOrdering = true)]  
        public IQueryable<V2FeedPackage> GetUpdates([FromODataUri] string packageIds, [FromODataUri] string versions, [FromODataUri] bool includePrerelease, [FromODataUri] bool includeAllVersions, [FromODataUri] string targetFrameworks = "", [FromODataUri] string versionConstraints = "")
        {
            if (String.IsNullOrEmpty(packageIds) || String.IsNullOrEmpty(versions))
            {
                return Enumerable.Empty<V2FeedPackage>().AsQueryable();
            }

            // Workaround https://github.com/NuGet/NuGetGallery/issues/674 for NuGet 2.1 client. Can probably eventually be retired (when nobody uses 2.1 anymore...)
            // Note - it was URI un-escaping converting + to ' ', undoing that is actually a pretty conservative substitution because
            // space characters are never acepted as valid by VersionUtility.ParseFrameworkName.
            if (!string.IsNullOrEmpty(targetFrameworks))
            {
                targetFrameworks = targetFrameworks.Replace(' ', '+');
            }

            var idValues = packageIds.Trim().ToLowerInvariant().Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
            var versionValues = versions.Trim().Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
            var targetFrameworkValues = String.IsNullOrEmpty(targetFrameworks)
                                            ? null
                                            : targetFrameworks.Split('|').Select(VersionUtility.ParseFrameworkName).ToList();
            var versionConstraintValues = String.IsNullOrEmpty(versionConstraints)
                                            ? new string[idValues.Length]
                                            : versionConstraints.Split('|');

            if (idValues.Length == 0 || idValues.Length != versionValues.Length || idValues.Length != versionConstraintValues.Length)
            {
                // Exit early if the request looks invalid
                return Enumerable.Empty<V2FeedPackage>().AsQueryable();
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

            return GetUpdates(packages, versionLookup, targetFrameworkValues, includeAllVersions).AsQueryable()
                .ToV2FeedPackageQuery(GetSiteRoot(), _configurationService.Features.FriendlyLicenses);
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