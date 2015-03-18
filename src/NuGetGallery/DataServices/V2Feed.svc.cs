using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Services;
using System.Linq;
using System.Runtime.Versioning;
using System.ServiceModel.Web;
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
            string[] targetFrameworkList = (targetFramework ?? "").Split(new[] {'\'', '|'}, StringSplitOptions.RemoveEmptyEntries);

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

            var packages = PackageRepository.GetAll()
                .Include(p => p.PackageRegistration)
                .Include(p => p.PackageRegistration.Owners)
                .Where(p => p.Listed);
            var query = SearchAdaptor.SearchCore(
                SearchService, 
                HttpContext.Request, 
                packages, 
                searchTerm, 
                targetFramework, 
                includePrerelease, 
                curatedFeed: null)
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
                    SemanticVersion currentVersion = null;
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

        public override Uri GetReadStreamUri(
            object entity,
            DataServiceOperationContext operationContext)
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
    }
}