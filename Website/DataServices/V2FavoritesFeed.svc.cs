using System;
using System.Data.Entity;
using System.Data.Services;
using System.Linq;
using System.Reflection;
using System.ServiceModel.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace NuGetGallery
{
    public class V2FavoritesFeed : FeedServiceBase<V2FeedPackage>
    {
        private const int FeedVersion = 2;

        protected IEntityRepository<PackageFavorite> FavoritesRepository { get; set; }

        public V2FavoritesFeed()
            : this(DependencyResolver.Current.GetService<IEntityRepository<PackageFavorite>>())
        {
        }

        public V2FavoritesFeed(IEntityRepository<PackageFavorite> favoritesRepo)
            : base()
        {
            this.FavoritesRepository = favoritesRepo;
        }

        public V2FavoritesFeed(IEntitiesContext entities, 
            IEntityRepository<Package> repo,
            IEntityRepository<PackageFavorite> favoritesRepo,
            IConfiguration configuration, ISearchService searchService)
            : base(entities, repo, configuration, searchService)
        {
            FavoritesRepository = favoritesRepo;
        }

        protected override FeedContext<V2FeedPackage> CreateDataSource()
        {
            var packages = GetPackages();

            return new FeedContext<V2FeedPackage>
                {
                    Packages = packages.ToV2FeedPackageQuery(Configuration.GetSiteRoot(UseHttps()))
                };
        }

        private IQueryable<Package> GetPackages()
        {
            string username = GetUserName();

            var packages = FavoritesRepository.GetAll()
                .Where(f => f.User.Username == username && f.IsFavorited)
                .SelectMany(f => f.PackageRegistration.Packages);

            return packages;
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

        private string GetUserName()
        {
            var curatedFeedName = HttpContext.Request.QueryString["name"];
            return curatedFeedName;
        }

        protected override void OnStartProcessingRequest(ProcessRequestArgs args)
        {
            FixUpDataServiceUrisForCuratedFeedName(args.OperationContext, GetUserName());
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "targetFramework", Justification = "We can't change it because it's now part of the contract of this service method.")]
        [WebGet]
        public IQueryable<V2FeedPackage> Search(string searchTerm, string targetFramework, bool includePrerelease)
        {
            IQueryable<Package> curatedPackages = GetPackages();
            return SearchAdaptor.SearchCore(SearchService, HttpContext.Request, SiteRoot, curatedPackages, searchTerm, targetFramework, includePrerelease, filterToPackageSet: curatedPackages)
                .ToV2FeedPackageQuery(Configuration.GetSiteRoot(UseHttps()));
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

        private static void FixUpDataServiceUrisForCuratedFeedName(
            DataServiceOperationContext operationContext,
            string curatedFeedName)
        {
            // AVERT YOUR EYES!

            // This is an *evil* hack to get proper URIs into the data servive's output, e.g. /api/v2/curated-feeds/{name}.
            // Without this, the URIs in the data service will be wrong, and won't work if a client tried to use them.

            var fixedUpSeriveUri = operationContext.AbsoluteServiceUri.AbsoluteUri.Replace(
                "/api/v2/favorites/", "/api/v2/favorites/" + curatedFeedName + "/");
            var fixedUpRequestUri = operationContext.AbsoluteRequestUri.AbsoluteUri.Replace(
                "/api/v2/favorites/", "/api/v2/favorites/" + curatedFeedName + "/");

            // The URI needs to be fixed up both on the actual IDataService host (hostInterface) and the service host wrapper (hostWrapper)
            // Null checks aren't really worth much here. If it does break, it'll result in a 500 to the client.
            var hostInterfaceField = operationContext.GetType().GetField("hostInterface", BindingFlags.NonPublic | BindingFlags.Instance);
            var hostInterface = hostInterfaceField.GetValue(operationContext);
            var hostWrapperField = operationContext.GetType().GetField("hostWrapper", BindingFlags.NonPublic | BindingFlags.Instance);
            var hostWrapper = hostWrapperField.GetValue(operationContext);

            // Fix up the service URIs
            var interfaceServiceUriField = hostInterface.GetType().GetField("absoluteServiceUri", BindingFlags.NonPublic | BindingFlags.Instance);
            interfaceServiceUriField.SetValue(hostInterface, new Uri(fixedUpSeriveUri));
            var wrapperServiceUriField = hostWrapper.GetType().GetField("absoluteServiceUri", BindingFlags.NonPublic | BindingFlags.Instance);
            wrapperServiceUriField.SetValue(hostWrapper, new Uri(fixedUpSeriveUri));

            // Fix up the request URIs
            var interfaceRequestUriField = hostInterface.GetType().GetField("absoluteRequestUri", BindingFlags.NonPublic | BindingFlags.Instance);
            interfaceRequestUriField.SetValue(hostInterface, new Uri(fixedUpRequestUri));
            var wrapperRequestUriField = hostWrapper.GetType().GetField("absoluteRequestUri", BindingFlags.NonPublic | BindingFlags.Instance);
            wrapperRequestUriField.SetValue(hostWrapper, new Uri(fixedUpRequestUri));

            // Take a shower.
        }
    }
}
