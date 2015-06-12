// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Services;
using System.Globalization;
using System.Linq;
using System.Runtime.Versioning;
using System.ServiceModel.Web;
using System.Web;
using System.Web.Caching;
using System.Web.Mvc;
using System.Web.Routing;
using NuGet;
using NuGetGallery.Configuration;
using NuGetGallery.DataServices;
using QueryInterceptor;

namespace NuGetGallery
{
    public class V2FeedContext : FeedContext<V2FeedPackage> { }

    public class V2Feed : FeedServiceBase<V2FeedContext, V2FeedPackage>
    {
        private const int FeedVersion = 2;
        private const int ServerCacheExpirationInSeconds = 30;

        public V2Feed()
        {
        }

        public V2Feed(IEntitiesContext entities, IEntityRepository<Package> repo, ConfigurationService configuration, ISearchService searchService)
            : base(entities, repo, configuration, searchService)
        {
        }

        protected override V2FeedContext CreateDataSource()
        {
            return new V2FeedContext
                {
                    Packages = PackageRepository
                        .GetAll()
                        .UseSearchService(SearchService, null, Configuration.GetSiteRoot(UseHttps()), Configuration.Features.FriendlyLicenses)
                        .WithoutVersionSort()
                        .ToV2FeedPackageQuery(Configuration.GetSiteRoot(UseHttps()), Configuration.Features.FriendlyLicenses)
                        .InterceptWith(new NormalizeVersionInterceptor())
                };
        }

        public static void InitializeService(DataServiceConfiguration config)
        {
            InitializeServiceBase(config);
            config.SetServiceOperationAccessRule("GetUpdates", ServiceOperationRights.AllRead);
        }

        [WebGet]
        public IQueryable<V2FeedPackage> Search(string searchTerm, string targetFramework, bool includePrerelease)
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

            // Check if the caller is requesting packages or calling the count operation.
            bool requestingCount = HttpContext.Request.RawUrl.Contains("$count");

            var isEmptySearchTerm = string.IsNullOrEmpty(searchTerm);
            if ((requestingCount && isEmptySearchTerm) || isEmptySearchTerm)
            {
                // Fetch the cache key for the empty search query.
                string cacheKey = GetCacheKeyForEmptySearchQuery(targetFramework, includePrerelease);

                IQueryable<V2FeedPackage> searchResults;
                DateTime lastModified;

                var cachedObject = HttpContext.Cache.Get(cacheKey);
                var currentDateTime = DateTime.UtcNow;
                if (cachedObject == null && !requestingCount)
                {
                    searchResults = SearchV2FeedCore(searchTerm, targetFramework, includePrerelease);

                    lastModified = currentDateTime;

                    // cache the most common search query
                    // note: this is per instance cache
                    var cachedPackages = searchResults.ToList();

                    // don't cache empty resulsets in case we missed any potential ODATA expressions
                    if (!cachedPackages.Any())
                    {
                        var cachedSearchResult = new CachedSearchResult();
                        cachedSearchResult.LastModified = currentDateTime;
                        cachedSearchResult.Packages = cachedPackages;

                        HttpContext.Cache.Add(cacheKey, cachedSearchResult, null,
                            currentDateTime.AddSeconds(ServerCacheExpirationInSeconds),
                            Cache.NoSlidingExpiration, CacheItemPriority.Default, null);
                    }
                }
                else if (cachedObject == null)
                {
                    // first hit on $count and nothing in cache yet;
                    // we can't cache due to the $count hijack in SearchV2FeedCore...
                    return SearchV2FeedCore(searchTerm, targetFramework, includePrerelease);
                }
                else
                {
                    var cachedSearchResult = (CachedSearchResult) cachedObject;
                    searchResults = cachedSearchResult.Packages.AsQueryable();
                    lastModified = cachedSearchResult.LastModified;
                }

                // Clients should cache twice as long.
                // This way, they won't notice differences in the short-lived per instance cache.
                HttpContext.Response.Cache.SetCacheability(HttpCacheability.Public);
                HttpContext.Response.Cache.SetMaxAge(TimeSpan.FromSeconds(60));
                HttpContext.Response.Cache.SetExpires(currentDateTime.AddSeconds(ServerCacheExpirationInSeconds * 2));
                HttpContext.Response.Cache.SetLastModified(lastModified);
                HttpContext.Response.Cache.SetValidUntilExpires(true);

                return searchResults;
            }

            return SearchV2FeedCore(searchTerm, targetFramework, includePrerelease);
        }

        private IQueryable<V2FeedPackage> SearchV2FeedCore(string searchTerm, string targetFramework, bool includePrerelease)
        {
            var packages = PackageRepository.GetAll()
                .Include(p => p.PackageRegistration)
                .Include(p => p.PackageRegistration.Owners)
                .Where(p => p.Listed);

            var query = SearchAdaptor.SearchCore(SearchService, HttpContext.Request, packages, searchTerm, targetFramework, includePrerelease, curatedFeed: null)
                // TODO: Async this when I can figure out OData async stuff...
                .Result
                .ToV2FeedPackageQuery(GetSiteRoot(), Configuration.Features.FriendlyLicenses);

            return query;
        }

        [WebGet]
        public IQueryable<V2FeedPackage> FindPackagesById(string id)
        {
            return PackageRepository.GetAll().Include(p => p.PackageRegistration)
                .Where(p => p.PackageRegistration.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
                .ToV2FeedPackageQuery(GetSiteRoot(), Configuration.Features.FriendlyLicenses);
        }

        [WebGet]
        public IQueryable<V2FeedPackage> GetUpdates(
            string packageIds,
            string versions,
            bool includePrerelease,
            bool includeAllVersions,
            string targetFrameworks,
            string versionConstraints)
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

            var packages = PackageRepository.GetAll()
                .Include(p => p.PackageRegistration)
                .Include(p => p.SupportedFrameworks)
                .Where(p =>
                    p.Listed && (includePrerelease || !p.IsPrerelease) &&
                    idValues.Contains(p.PackageRegistration.Id.ToLower()))
                .OrderBy(p => p.PackageRegistration.Id);

            return GetUpdates(packages, versionLookup, targetFrameworkValues, includeAllVersions).AsQueryable()
                .ToV2FeedPackageQuery(GetSiteRoot(), Configuration.Features.FriendlyLicenses);
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

        public override Uri GetReadStreamUri(object entity, DataServiceOperationContext operationContext)
        {
            var package = (V2FeedPackage)entity;
            var urlHelper = new UrlHelper(new RequestContext(HttpContext, new RouteData()));

            string url = urlHelper.PackageDownload(FeedVersion, package.Id, package.NormalizedVersion);

            return new Uri(url, UriKind.Absolute);
        }

        private string GetSiteRoot()
        {
            return Configuration.GetSiteRoot(UseHttps());
        }

        /// <summary>
        /// The most common search queries should be cached and yield a cache-key.
        /// </summary>
        /// <param name="targetFramework">The target framework.</param>
        /// <param name="includePrerelease"><code>True</code>, to include prereleases; otherwise <code>false</code>.</param>
        /// <returns>The cache key for the specified search criteria.</returns>
        private static string GetCacheKeyForEmptySearchQuery(string targetFramework, bool includePrerelease)
        {
            string cacheKeyFormat = "commonquery_v2_{0}_{1}";

            string targetFrameworkKey = targetFramework.ToLowerInvariant();
            if (string.IsNullOrEmpty(targetFramework))
            {
                targetFrameworkKey = "noframework";
            }

            string prereleaseKey = "excl";
            if (includePrerelease)
            {
                prereleaseKey = "incl";
            }

            return string.Format(CultureInfo.InvariantCulture, cacheKeyFormat, targetFrameworkKey, prereleaseKey);
        }

        private class CachedSearchResult
        {
            public DateTime LastModified { get; set; }
            public List<V2FeedPackage> Packages { get; set; }
        }
    }
}