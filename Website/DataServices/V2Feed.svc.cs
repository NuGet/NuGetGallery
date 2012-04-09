using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Services;
using System.Linq;
using System.Runtime.Versioning;
using System.ServiceModel.Web;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using NuGet;

namespace NuGetGallery
{
    public class V2Feed : FeedServiceBase<V2FeedPackage>
    {
        private const int FeedVersion = 2;

        public V2Feed()
        {

        }

        public V2Feed(IEntitiesContext entities, IEntityRepository<Package> repo, IConfiguration configuration, ISearchService searchSvc)
            : base(entities, repo, configuration, searchSvc)
        {

        }

        protected override FeedContext<V2FeedPackage> CreateDataSource()
        {
            return new FeedContext<V2FeedPackage>
            {
                Packages = PackageRepo.GetAll()
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
            // Filter out unlisted packages when searching. We will return it when a generic "GetPackages" request comes and filter it on the client.
            var packages = PackageRepo.GetAll().Where(p => p.Listed);
            if (!includePrerelease)
            {
                packages = packages.Where(p => !p.IsPrerelease);
            }
            return packages.Search(searchTerm)
                           .ToV2FeedPackageQuery(GetSiteRoot());
        }

        [WebGet]
        public IQueryable<V2FeedPackage> FindPackagesById(string id)
        {
            return PackageRepo.GetAll().Include(p => p.PackageRegistration)
                                       .Where(p => p.PackageRegistration.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
                                       .ToV2FeedPackageQuery(GetSiteRoot());
        }

        [WebGet]
        public IQueryable<V2FeedPackage> GetUpdates(string packageIds, string versions, bool includePrerelease, bool includeAllVersions, string targetFrameworks)
        {
            var idValues = packageIds.Split('|');
            var versionValues = versions.Split('|');
            var targetFrameworkValues = String.IsNullOrEmpty(targetFrameworks) ? null :
                                                                                 targetFrameworks.Split('|').Select(VersionUtility.ParseFrameworkName).ToList();

            if (!idValues.Any() || !idValues.Count().Equals(versionValues.Count()))
            {
                throw new InvalidOperationException("Number of packages do not match versions");
            }

            Dictionary<string, SemanticVersion> versionLookup = new Dictionary<string, SemanticVersion>(idValues.Count(), StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < idValues.Length; i++)
            {
                var id = idValues[i];
                SemanticVersion version;
                SemanticVersion currentVersion = new SemanticVersion(versionValues[i]);
                if (!versionLookup.TryGetValue(id, out version) || (currentVersion > version))
                {
                    // If we've never added the package to lookup or we encounter the same id but with a higher version, then choose the higher version.
                    versionLookup[id] = currentVersion;
                }
            }

            var packages = PackageRepo.GetAll()
                              .Include(p => p.PackageRegistration)
                              .Include(p => p.SupportedFrameworks)
                              .Where(p => p.Listed && (includePrerelease || !p.IsPrerelease) && idValues.Contains(p.PackageRegistration.Id))
                              .OrderBy(p => p.PackageRegistration.Id);
            return GetUpdates(packages, versionLookup, targetFrameworkValues, includeAllVersions).AsQueryable()
                                                                                                 .ToV2FeedPackageQuery(GetSiteRoot());
        }

        private static IEnumerable<Package> GetUpdates(IEnumerable<Package> packages,
                                                       Dictionary<string, SemanticVersion> versionLookup,
                                                       IEnumerable<FrameworkName> targetFrameworkValues,
                                                       bool includeAllVersions)
        {
            var updates = packages.AsEnumerable()
                                  .Where(p =>
                                  {
                                      // For each package, if the version is higher than the client version and we satisty the target framework, return it.
                                      // TODO: We could optimize for the includeAllVersions case here by short circuiting the operation once we've encountered the highest version
                                      // for a given Id
                                      var version = SemanticVersion.Parse(p.Version);
                                      var clientVersion = versionLookup[p.PackageRegistration.Id];
                                      var supportedPackageFrameworks = p.SupportedFrameworks.Select(f => f.FrameworkName);

                                      return (version > clientVersion) &&
                                              (targetFrameworkValues == null || targetFrameworkValues.Any(s => VersionUtility.IsCompatible(s, supportedPackageFrameworks)));
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
            var httpContext = new HttpContextWrapper(HttpContext.Current);
            var urlHelper = new UrlHelper(new RequestContext(httpContext, new RouteData()));

            string url = urlHelper.PackageDownload(FeedVersion, package.Id, package.Version);

            return new Uri(url, UriKind.Absolute);
        }

        private string GetSiteRoot()
        {
            return Configuration.GetSiteRoot(UseHttps());
        }
    }
}
