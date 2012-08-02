using System;
using System.Data.Entity;
using System.Data.Services;
using System.Linq;
using System.ServiceModel.Web;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace NuGetGallery
{
    public class V1Feed : FeedServiceBase<V1FeedPackage>
    {
        private const int FeedVersion = 1;
        public V1Feed()
        {

        }

        public V1Feed(IEntitiesContext entities, IEntityRepository<Package> repo, IConfiguration configuration, ISearchService searchSvc)
            : base(entities, repo, configuration, searchSvc)
        {

        }

        public static void InitializeService(DataServiceConfiguration config)
        {
            InitializeServiceBase(config);
        }

        protected internal override IQueryable<Package> GetPackages()
        {
            return base.GetPackages().Where(p => !p.IsPrerelease);
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
        public IQueryable<V1FeedPackage> Search(string searchTerm, string targetFramework)
        {
            var packages = PackageRepo.GetAll()
                                      .Include(p => p.PackageRegistration)
                                      .Where(p => !p.IsPrerelease);
            if (!String.IsNullOrEmpty(searchTerm))
            {
                // For v1 feed, only allow stable package versions.
                packages = SearchCore(searchTerm, targetFramework, includePrerelease: false);
            }
            return ToFeedPackage(packages);
        }

        protected override IQueryable<V1FeedPackage> ToFeedPackage(IQueryable<Package> packages)
        {
            return packages.ToV1FeedPackageQuery(SiteRoot);
        }
    }
}
