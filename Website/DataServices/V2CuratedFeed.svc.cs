using System;
using System.Data.Entity;
using System.Data.Services;
using System.Linq;
using System.Reflection;
using System.ServiceModel.Web;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace NuGetGallery
{
    // TODO : Have V2CuratedFeed derive from V2Feed
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
                Packages = packages.ToV2FeedPackageQuery(Configuration.GetSiteRoot(UseHttps()))
            };
        }

        public static void InitializeService(DataServiceConfiguration config)
        {
            InitializeServiceBase(config);
        }

        [WebGet]
        public IQueryable<V2FeedPackage> FindPackagesById(string id)
        {
            return GetPackages()
                .Where(p => p.PackageRegistration.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
                .ToV2FeedPackageQuery(Configuration.GetSiteRoot(UseHttps()));
        }

        private static void FixUpDataServiceUrisForCuratedFeedName(
            DataServiceOperationContext operationContext,
            string curatedFeedName)
        {
            // AVERT YOUR EYES!

            // This is an *evil* hack to get proper URIs into the data servive's output, e.g. /api/v2/curated-feeds/{name}.
            // Without this, the URIs in the data service will be wrong, and won't work if a client tried to use them.
            
            var fixedUpSeriveUri = operationContext.AbsoluteServiceUri.AbsoluteUri.Replace("/api/v2/curated-feed/", "/api/v2/curated-feeds/" + curatedFeedName + "/");
            var fixedUpRequestUri = operationContext.AbsoluteRequestUri.AbsoluteUri.Replace("/api/v2/curated-feed/", "/api/v2/curated-feeds/" + curatedFeedName + "/");

            // The URI needs to be fixed up both on the actual IDataService host (hostInterface) and the service host wrapper (hostWrapper)
            // Null checks aren't really worth much here. If it does break, it'll result in a 500 to the client.
            var hostInterfaceField = operationContext.GetType().GetField("hostInterface",BindingFlags.NonPublic | BindingFlags.Instance);
            var hostInterface = hostInterfaceField.GetValue(operationContext);
            var hostWrapperField = operationContext.GetType().GetField("hostWrapper", BindingFlags.NonPublic | BindingFlags.Instance);
            var hostWrapper = hostWrapperField.GetValue(operationContext);

            // Fix up the service URIs
            var interfaceServiceUriField = hostInterface.GetType().GetField("absoluteServiceUri", BindingFlags.NonPublic | BindingFlags.Instance);
            interfaceServiceUriField.SetValue(hostInterface, new Uri(fixedUpSeriveUri));
            var wrapperServiceUriField = hostWrapper.GetType().GetField("absoluteServiceUri", BindingFlags.NonPublic | BindingFlags.Instance);
            wrapperServiceUriField.SetValue(hostWrapper, new Uri(fixedUpSeriveUri));
            
            // Fix up the request URIs
            var interfaceRequestUriField = hostInterface.GetType().GetField("absoluteRequestUri",BindingFlags.NonPublic | BindingFlags.Instance);
            interfaceRequestUriField.SetValue(hostInterface, new Uri(fixedUpRequestUri));
            var wrapperRequestUriField = hostWrapper.GetType().GetField("absoluteRequestUri", BindingFlags.NonPublic | BindingFlags.Instance);
            wrapperRequestUriField.SetValue(hostWrapper, new Uri(fixedUpRequestUri));
            
            // Take a shower.
        }

        private string GetCuratedFeedName()
        {
            var curatedFeedName = HttpContext.Request.QueryString["name"];

            var curatedFeed = Entities.CuratedFeeds.SingleOrDefault(cf => cf.Name == curatedFeedName);
            if (curatedFeed == null)
                throw new DataServiceException(404, "Not Found");

            return curatedFeedName;
        }

        private IQueryable<Package> GetPackages()
        {
            var curatedFeedName = GetCuratedFeedName();

            var packages = Entities.CuratedFeeds
                                    .Where(cf => cf.Name == curatedFeedName)
                                    .Include(cf => cf.Packages.Select(cp => cp.PackageRegistration.Packages))
                                    .SelectMany(cf => cf.Packages.SelectMany(cp => cp.PackageRegistration.Packages.Select(p => p)));

            // The curated feeds table has duplicate entries for feed, package registration pairs. Consequently
            // we have to apply a distinct on the results.
            return packages.Distinct();
        }

        protected override void OnStartProcessingRequest(ProcessRequestArgs args)
        {
            FixUpDataServiceUrisForCuratedFeedName(args.OperationContext, GetCuratedFeedName());
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
            return packages.Search(searchTerm).ToV2FeedPackageQuery(Configuration.GetSiteRoot(UseHttps()));
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
    }
}
