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
using NuGetGallery.Helpers;

namespace NuGetGallery
{
    public class V2Feed : FeedServiceBase<V2FeedPackage>
    {
        private const int FeedVersion = 2;

        public V2Feed()
        {
        }

        public V2Feed(IEntitiesContext entities, IEntityRepository<Package> repo, ConfigurationService configuration, ISearchService searchService)
            : base(entities, repo, configuration, searchService)
        {
        }

        protected override FeedContext<V2FeedPackage> CreateDataSource()
        {
            return new FeedContext<V2FeedPackage>
                {
                    Packages = PackageRepository.GetAll()
                        .WithoutVersionSort()
                        .ToV2FeedPackageQuery(Configuration.GetSiteRoot(UseHttps()))
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
            var packages = PackageRepository.GetAll()
                .Include(p => p.PackageRegistration)
                .Include(p => p.PackageRegistration.Owners)
                .Where(p => p.Listed);
            return SearchAdaptor.SearchCore(SearchService, HttpContext.Request, SiteRoot, packages, searchTerm, targetFramework, includePrerelease, curatedFeedKey: null).ToV2FeedPackageQuery(GetSiteRoot());
        }

        [WebGet]
        public IQueryable<V2FeedPackage> FindPackagesById(string id)
        {
            return PackageRepository.GetAll().Include(p => p.PackageRegistration)
                .Where(p => p.PackageRegistration.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
                .ToV2FeedPackageQuery(GetSiteRoot());
        }

        [WebGet]
        public IQueryable<V2FeedPackage> GetUpdates(
            string packageIds, string versions, bool includePrerelease, bool includeAllVersions, string targetFrameworks, string versionConstraints)
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

            var idValues = packageIds.Trim().Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
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

            var versionLookup = new Dictionary<string, Tuple<SemanticVersion, IVersionSpec>>(idValues.Length, StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < idValues.Length; i++)
            {
                var id = idValues[i];

                if (versionLookup.ContainsKey(id))
                {
                    // Exit early if the request contains duplicate ids
                    return Enumerable.Empty<V2FeedPackage>().AsQueryable();
                }

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
                    versionLookup.Add(id, Tuple.Create(currentVersion, versionConstraint));
                }
            }

            var packages = PackageRepository.GetAll()
                .Include(p => p.PackageRegistration)
                .Include(p => p.SupportedFrameworks)
                .Where(p => p.Listed && (includePrerelease || !p.IsPrerelease) && idValues.Contains(p.PackageRegistration.Id))
                .OrderBy(p => p.PackageRegistration.Id);
            return GetUpdates(packages, versionLookup, targetFrameworkValues, includeAllVersions).AsQueryable()
                .ToV2FeedPackageQuery(GetSiteRoot());
        }

        private static IEnumerable<Package> GetUpdates(
            IEnumerable<Package> packages,
            Dictionary<string, Tuple<SemanticVersion, IVersionSpec>> versionLookup,
            IEnumerable<FrameworkName> targetFrameworkValues,
            bool includeAllVersions)
        {
            var updates = packages.AsEnumerable()
                .Where(
                    p =>
                        {
                            // For each package, if the version is higher than the client version and we satisty the target framework, return it.
                            // TODO: We could optimize for the includeAllVersions case here by short circuiting the operation once we've encountered the highest version
                            // for a given Id
                            var version = SemanticVersion.Parse(p.Version);
                            Tuple<SemanticVersion, IVersionSpec> versionTuple;
                            
                            if (versionLookup.TryGetValue(p.PackageRegistration.Id, out versionTuple))
                            {
                                SemanticVersion clientVersion = versionTuple.Item1;
                                var supportedPackageFrameworks = p.SupportedFrameworks.Select(f => f.FrameworkName);

                                IVersionSpec versionConstraint = versionTuple.Item2;

                                return (version > clientVersion) &&
                                       (targetFrameworkValues == null || 
                                        targetFrameworkValues.Any(s => VersionUtility.IsCompatible(s, supportedPackageFrameworks))) &&
                                        (versionConstraint == null || versionConstraint.Satisfies(version));
                            }
                            return false;
                        });

            if (!includeAllVersions)
            {
                return updates.GroupBy(p => p.PackageRegistration.Id)
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

            string url = urlHelper.PackageDownload(FeedVersion, package.Id, package.Version);

            return new Uri(url, UriKind.Absolute);
        }

        private string GetSiteRoot()
        {
            return Configuration.GetSiteRoot(UseHttps());
        }
    }
}