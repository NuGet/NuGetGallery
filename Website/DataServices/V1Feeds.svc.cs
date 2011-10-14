using System;
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
        protected override FeedContext<V1FeedPackage> CreateDataSource()
        {
            return new FeedContext<V1FeedPackage>
            {
                Packages = PackageRepo.GetAll()
                                      .Where(p => !p.IsPrerelease)
                                      .ToV1FeedPackageQuery()
            };
        }

        public override Uri GetReadStreamUri(
           object entity,
           DataServiceOperationContext operationContext)
        {

            var package = (V1FeedPackage)entity;
            var httpContext = new HttpContextWrapper(HttpContext.Current);
            var urlHelper = new UrlHelper(new RequestContext(httpContext, new RouteData()));

            string url = urlHelper.PackageDownload(package.Id, package.Version);

            return new Uri(url, UriKind.Absolute);
        }

        [WebGet]
        public IQueryable<V1FeedPackage> Search(string searchTerm, string targetFramework)
        {
            // Only allow listed stable releases to be returned when searching the v1 feed.
            return PackageRepo.GetAll()
                              .Where(p => !p.IsPrerelease && p.Listed)
                              .Search(searchTerm)
                              .ToV1FeedPackageQuery();
        }
    }
}
