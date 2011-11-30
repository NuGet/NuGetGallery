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
    public class V2Feed : FeedServiceBase<V2FeedPackage>
    {
        private const int FeedVersion = 2;

        public V2Feed()
        {

        }

        public V2Feed(IEntityRepository<Package> repo, IConfiguration configuration, ISearchService searchSvc)
            : base(repo, configuration, searchSvc)
        {

        }

        protected override FeedContext<V2FeedPackage> CreateDataSource()
        {
            return new FeedContext<V2FeedPackage>
            {
                Packages = PackageRepo.GetAll()
                                      .ToV2FeedPackageQuery(Configuration.SiteRoot)
            };
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
                           .ToV2FeedPackageQuery(Configuration.SiteRoot);
        }

        [WebGet]
        public IQueryable<V2FeedPackage> FindPackagesById(string id)
        {
            return PackageRepo.GetAll().Include(p => p.PackageRegistration)
                                       .Where(p => p.PackageRegistration.Id.Equals(id, StringComparison.OrdinalIgnoreCase) && p.Listed)
                                       .ToV2FeedPackageQuery(Configuration.SiteRoot);
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
    }
}
