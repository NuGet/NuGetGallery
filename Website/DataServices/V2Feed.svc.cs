using System;
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
        protected override FeedContext<V2FeedPackage> CreateDataSource()
        {
            return new FeedContext<V2FeedPackage>
            {
                Packages = PackageRepo.GetAll()
                                      .ToV2FeedPackageQuery()
            };
        }

        
        public IQueryable<V2FeedPackage> Search(string searchTerm, string targetFramework, bool allowPrereleasePackages)
        {
            // Filter out unlisted packages when searching. We will return it when a generic "GetPackages" request comes and filter it on the client.
            // Since this is used by old clients, we'll always filter out prerelease packages.
            return PackageRepo.GetAll()
                              .Where(p => p.Listed && !p.IsPrerelease)
                              .Search(searchTerm)
                              .ToV2FeedPackageQuery();
        }

        public override Uri GetReadStreamUri(
           object entity,
           DataServiceOperationContext operationContext)
        {

            var package = (V2FeedPackage)entity;
            var httpContext = new HttpContextWrapper(HttpContext.Current);
            var urlHelper = new UrlHelper(new RequestContext(httpContext, new RouteData()));

            string url = urlHelper.PackageDownload(package.Id, package.Version);

            return new Uri(url, UriKind.Absolute);
        }
    }
}
