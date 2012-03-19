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
    public class V2CuratedFeed : FeedServiceBase<V2FeedPackage>
    {
        private const int FeedVersion = 2;

        public V2CuratedFeed()
        {

        }

        public V2CuratedFeed(IEntitiesContext entities, IEntityRepository<Package> repo, IConfiguration configuration, ISearchService searchSvc)
            : base(entities, repo, configuration, searchSvc)
        {
        }

        protected override FeedContext<V2FeedPackage> CreateDataSource()
        {
            var packages = GetPackages();
            
            return new FeedContext<V2FeedPackage>
            {
                Packages = packages.ToV2FeedPackageQuery(Configuration.SiteRoot)
            };
        }

        [WebGet]
        public IQueryable<V2FeedPackage> FindPackagesById(string id)
        {
            return GetPackages()
                .Where(p => p.PackageRegistration.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
                .ToV2FeedPackageQuery(Configuration.SiteRoot);
        }

        public IQueryable<Package> GetPackages()
        {
            string curatedFeed = HttpContext.Current.Request.QueryString["name"];
            if (curatedFeed == null)
                throw new Exception();

            return Entities.CuratedFeeds
                .Where(cf => cf.Name == curatedFeed)
                .Include(cf => cf.Packages.Select(cp => cp.PackageRegistration.Packages))
                .SelectMany(cf => cf.Packages.SelectMany(cp => cp.PackageRegistration.Packages.Select(p => p)));
        }

        [WebGet]
        public IQueryable<V2FeedPackage> Search(string searchTerm, string targetFramework, bool includePrerelease)
        {
            var packages = GetPackages();

            packages = packages.Where(p => p.Listed);
            if (!includePrerelease)
            {
                packages = packages.Where(p => !p.IsPrerelease);
            }
            return packages.Search(searchTerm).ToV2FeedPackageQuery(Configuration.SiteRoot);
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
