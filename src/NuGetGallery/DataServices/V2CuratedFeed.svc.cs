using System;
using System.Data.Entity;
using System.Data.Services;
using System.Linq;
using System.Reflection;
using System.ServiceModel.Web;
using System.Web.Mvc;
using System.Web.Routing;
using NuGetGallery;
using NuGetGallery.Configuration;
using QueryInterceptor;

namespace NuGetGallery
{
    // TODO : Have V2CuratedFeed derive from V2Feed?
    [RewriteBaseUrlMessageInspector]
    public class V2CuratedFeed : FeedServiceBase<V2FeedContext, V2FeedPackage>
    {
        private const int FeedVersion = 2;

        private ICuratedFeedService _curatedFeedService;

        public V2CuratedFeed()
            : this(DependencyResolver.Current.GetService<ICuratedFeedService>())
        {
        }

        public V2CuratedFeed(ICuratedFeedService curatedFeedService)
            : base()
        {
            _curatedFeedService = curatedFeedService;
        }

        public V2CuratedFeed(IEntitiesContext entities, IEntityRepository<Package> repo, ConfigurationService configuration, ISearchService searchService, ICuratedFeedService curatedFeedService)
            : base(entities, repo, configuration, searchService)
        {
            _curatedFeedService = curatedFeedService;
        }

        protected override V2FeedContext CreateDataSource()
        {
            var curatedFeedName = GetCuratedFeedName();

            if (!Entities.CuratedFeeds.Any(cf => cf.Name == curatedFeedName))
            {
                throw new DataServiceException(404, "Not Found");
            }

            var packages = _curatedFeedService.GetPackages(curatedFeedName);

            return new V2FeedContext
                {
                    Packages = packages
                        .ToV2FeedPackageQuery(Configuration.GetSiteRoot(UseHttps()), Configuration.Features.FriendlyLicenses)
                        .InterceptWith(new NormalizeVersionInterceptor())
                };
        }

        public static void InitializeService(DataServiceConfiguration config)
        {
            InitializeServiceBase(config);
        }

        [WebGet]
        public IQueryable<V2FeedPackage> FindPackagesById(string id)
        {
            var curatedFeedName = GetCuratedFeedName();
            return _curatedFeedService.GetPackages(curatedFeedName)
                .Where(p => p.PackageRegistration.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
                .ToV2FeedPackageQuery(Configuration.GetSiteRoot(UseHttps()), Configuration.Features.FriendlyLicenses);
        }

        private string GetCuratedFeedName()
        {
            var curatedFeedName = HttpContext.Request.QueryString["name"];
            return curatedFeedName;
        }

        private IQueryable<Package> GetPackages()
        {
            var curatedFeedName = GetCuratedFeedName();
            return _curatedFeedService.GetPackages(curatedFeedName);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "targetFramework", Justification = "We can't change it because it's now part of the contract of this service method.")]
        [WebGet]
        public IQueryable<V2FeedPackage> Search(string searchTerm, string targetFramework, bool includePrerelease)
        {
            var curatedFeedName = GetCuratedFeedName();
            var curatedFeed = _curatedFeedService.GetFeedByName(curatedFeedName, includePackages: false);
            if (curatedFeed == null)
            {
                throw new DataServiceException(404, "Not Found");
            }

            var curatedPackages = _curatedFeedService.GetPackages(curatedFeedName);

            return SearchAdaptor.SearchCore(
                SearchService, 
                HttpContext.Request, 
                curatedPackages, 
                searchTerm, 
                targetFramework, 
                includePrerelease, 
                curatedFeed)
                // TODO: Async this when I can figure out OData async stuff...
                .Result
                .ToV2FeedPackageQuery(Configuration.GetSiteRoot(UseHttps()), Configuration.Features.FriendlyLicenses);
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