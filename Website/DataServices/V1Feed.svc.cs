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

        public V1Feed(IEntityRepository<Package> repo, IConfiguration configuration, ISearchService searchSvc)
            : base(repo, configuration, searchSvc)
        {

        }

        protected override FeedContext<V1FeedPackage> CreateDataSource()
        {
            return new FeedContext<V1FeedPackage>
            {
                Packages = PackageRepo.GetAll()
                                      .Where(p => !p.IsPrerelease)
                                      .ToV1FeedPackageQuery(Configuration.SiteRoot)
            };
        }

        public override Uri GetReadStreamUri(
           object entity,
           DataServiceOperationContext operationContext)
        {
            var package = (V1FeedPackage)entity;
            var httpContext = new HttpContextWrapper(HttpContext.Current);
            var urlHelper = new UrlHelper(new RequestContext(httpContext, new RouteData()));

            string url = urlHelper.PackageDownload(FeedVersion, package.Id, package.Version);

            return new Uri(url, UriKind.Absolute);
        }

        [WebGet]
        public IQueryable<V1FeedPackage> FindPackagesById(string id)
        {
            return PackageRepo.GetAll().Include(p => p.PackageRegistration)
                                       .Where(p => !p.IsPrerelease && p.PackageRegistration.Id.Equals(id, StringComparison.OrdinalIgnoreCase) && p.Listed)
                                       .ToV1FeedPackageQuery(Configuration.SiteRoot);
        }

        [WebGet]
        public IQueryable<V1FeedPackage> Search(string searchTerm, string targetFramework)
        {
            // Only allow listed stable releases to be returned when searching the v1 feed.
            var packages = PackageRepo.GetAll().Where(p => !p.IsPrerelease && p.Listed);

            if (String.IsNullOrEmpty(searchTerm))
            {
                return packages.ToV1FeedPackageQuery(Configuration.SiteRoot);
            }
            return SearchService.Search(packages, searchTerm)
                                .ToV1FeedPackageQuery(Configuration.SiteRoot);
        }
    }
}
