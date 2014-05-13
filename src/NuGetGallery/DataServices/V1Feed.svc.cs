using System;
using System.Data.Entity;
using System.Data.Services;
using System.Linq;
using System.ServiceModel.Web;
using System.Web.Mvc;
using System.Web.Routing;
using NuGetGallery.Configuration;

namespace NuGetGallery
{
    public class V1FeedContext : FeedContext<V1FeedPackage> { }

    public class V1Feed : FeedServiceBase<V1FeedContext, V1FeedPackage>
    {
        private const int FeedVersion = 1;

        public V1Feed()
        {
        }

        public V1Feed(IEntitiesContext entities, IEntityRepository<Package> repo, ConfigurationService configuration, ISearchService searchService)
            : base(entities, repo, configuration, searchService)
        {
        }

        public static void InitializeService(DataServiceConfiguration config)
        {
            InitializeServiceBase(config);
        }

        protected override V1FeedContext CreateDataSource()
        {
            return new V1FeedContext
                {
                    Packages = PackageRepository.GetAll()
                        .Where(p => !p.IsPrerelease)
                        .WithoutVersionSort()
                        .ToV1FeedPackageQuery(Configuration.GetSiteRoot(UseHttps()))
                };
        }

        public override Uri GetReadStreamUri(
            object entity,
            DataServiceOperationContext operationContext)
        {
            var package = (V1FeedPackage)entity;
            var urlHelper = new UrlHelper(new RequestContext(HttpContext, new RouteData()));

            string url = urlHelper.PackageDownload(FeedVersion, package.Id, package.Version);

            return new Uri(url, UriKind.Absolute);
        }

        [WebGet]
        public IQueryable<V1FeedPackage> FindPackagesById(string id)
        {
            return PackageRepository.GetAll().Include(p => p.PackageRegistration)
                .Where(p => !p.IsPrerelease && p.PackageRegistration.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
                .ToV1FeedPackageQuery(Configuration.GetSiteRoot(UseHttps()));
        }

        [WebGet]
        public IQueryable<V1FeedPackage> Search(string searchTerm, string targetFramework)
        {
            var packages = PackageRepository.GetAll()
                .Include(p => p.PackageRegistration)
                .Include(p => p.PackageRegistration.Owners)
                .Where(p => p.Listed && !p.IsPrerelease);

            // For v1 feed, only allow stable package versions.
            packages = SearchAdaptor.SearchCore(
                SearchService, 
                HttpContext.Request, 
                packages, 
                searchTerm, 
                targetFramework, 
                includePrerelease: false, 
                curatedFeed: null)
                // TODO: Async once I figure Odata Async stuff out
                .Result;
            return packages.ToV1FeedPackageQuery(Configuration.GetSiteRoot(UseHttps()));
        }
    }
}