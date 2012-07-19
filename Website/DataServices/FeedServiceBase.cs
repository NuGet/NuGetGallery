using System;
using System.Data.Entity;
using System.Data.Services;
using System.Data.Services.Common;
using System.Data.Services.Providers;
using System.IO;
using System.Linq;
using System.ServiceModel;
using System.Web;
using System.Web.Mvc;
using QueryInterceptor;

namespace NuGetGallery
{
    [ServiceBehavior(IncludeExceptionDetailInFaults = true, ConcurrencyMode = ConcurrencyMode.Multiple)]
    public abstract class FeedServiceBase<TPackage> : DataService<FeedContext<TPackage>>, IDataServiceStreamProvider, IServiceProvider
    {
        private readonly IEntitiesContext entities;
        private readonly IEntityRepository<Package> packageRepo;
        private readonly IConfiguration configuration;
        private readonly ISearchService searchService;

        public FeedServiceBase()
            : this(DependencyResolver.Current.GetService<IEntitiesContext>(),
                   DependencyResolver.Current.GetService<IEntityRepository<Package>>(),
                   DependencyResolver.Current.GetService<IConfiguration>(),
                   DependencyResolver.Current.GetService<ISearchService>())
        {

        }

        protected FeedServiceBase(
            IEntitiesContext entities,
            IEntityRepository<Package> packageRepo,
            IConfiguration configuration,
            ISearchService searchService)
        {
            this.entities = entities;
            this.packageRepo = packageRepo;
            this.configuration = configuration;
            this.searchService = searchService;
        }

        protected IEntitiesContext Entities
        {
            get { return entities; }
        }

        protected IEntityRepository<Package> PackageRepo
        {
            get { return packageRepo; }
        }

        protected IConfiguration Configuration
        {
            get { return configuration; }
        }

        protected ISearchService SearchService
        {
            get { return searchService; }
        }

        // This method is called only once to initialize service-wide policies.
        protected static void InitializeServiceBase(DataServiceConfiguration config)
        {
            config.SetServiceOperationAccessRule("Search", ServiceOperationRights.AllRead);
            config.SetServiceOperationAccessRule("FindPackagesById", ServiceOperationRights.AllRead);
            config.SetEntitySetAccessRule("Packages", EntitySetRights.AllRead);
            config.SetEntitySetPageSize("Packages", 100);
            config.DataServiceBehavior.MaxProtocolVersion = DataServiceProtocolVersion.V2;
            config.UseVerboseErrors = true;
        }

        protected abstract override FeedContext<TPackage> CreateDataSource();

        public void DeleteStream(
            object entity,
            DataServiceOperationContext operationContext)
        {
            throw new NotSupportedException();
        }

        public Stream GetReadStream(
            object entity,
            string etag,
            bool? checkETagForEquality,
            DataServiceOperationContext operationContext)
        {
            throw new NotSupportedException();
        }

        public abstract Uri GetReadStreamUri(
            object entity,
            DataServiceOperationContext operationContext);


        public string GetStreamContentType(
            object entity,
            DataServiceOperationContext operationContext)
        {
            return "application/zip";
        }

        public string GetStreamETag(
            object entity,
            DataServiceOperationContext operationContext)
        {
            return null;
        }

        public Stream GetWriteStream(
            object entity,
            string etag,
            bool? checkETagForEquality,
            DataServiceOperationContext operationContext)
        {
            throw new NotSupportedException();
        }

        public string ResolveType(
            string entitySetName,
            DataServiceOperationContext operationContext)
        {
            throw new NotSupportedException();
        }

        public int StreamBufferSize
        {
            get { return 64000; }
        }

        public object GetService(Type serviceType)
        {
            if (serviceType == typeof(IDataServiceStreamProvider))
            {
                return this;
            }

            return null;
        }

        protected virtual IQueryable<Package> SearchCore(string searchTerm, string targetFramework, bool includePrerelease)
        {
            // Filter out unlisted packages when searching. We will return it when a generic "GetPackages" request comes and filter it on the client.
            var packages = PackageRepo.GetAll()
                                      .Include(p => p.PackageRegistration)
                                      .Include(x => x.Authors)
                                      .Include(x => x.PackageRegistration.Owners)
                                      .Where(p => p.Listed);

            if (String.IsNullOrEmpty(searchTerm))
            {
                return packages;
            }

            var request = new HttpRequestWrapper(HttpContext.Current.Request);
            SearchFilter searchFilter;
            
            // We can only use Lucene if the client queries for the latest versions (IsLatest \ IsLatestStable) versions of a package.
            if (TryReadSearchFilter(request, out searchFilter))
            {
                searchFilter.SearchTerm = searchTerm;
                searchFilter.IncludePrerelease = includePrerelease;

                return GetResultsFromSearchService(packages, searchFilter);
            }
            return packages.Search(searchTerm);
        }

        private IQueryable<Package> GetResultsFromSearchService(IQueryable<Package> packages, SearchFilter searchFilter)
        {
            int totalHits = 0;
            var result = SearchService.Search(packages, searchFilter, out totalHits);

            // For count queries, we can ask the SearchService to not filter the source results. This would avoid hitting the database and consequently make
            // it very fast.
            if (searchFilter.CountOnly)
            {
                // At this point, we already know what the total count is. We can have it return this value very quickly without doing any SQL.
                return result.InterceptWith(new CountInterceptor(totalHits));
            }
            
            // For relevance search, Lucene returns us a paged\sorted list. OData tries to apply default ordering and Take \ Skip on top of this.
            // We avoid it by yanking these expressions out of out the tree.
            return result.InterceptWith(new DisregardODataInterceptor());
        }

        private bool TryReadSearchFilter(HttpRequestBase request, out SearchFilter searchFilter)
        {
            searchFilter = new SearchFilter 
            {
                Take = ReadInt(request["$top"], 30),
                Skip = ReadInt(request["$skip"], 0),
                CountOnly = request.Path.TrimEnd('/').EndsWith("$count")
            };

            switch(request["$orderby"])
            {
                case "DownloadCount desc,Id":
                    searchFilter.SortProperty = SortProperty.DownloadCount;
                    break;
                case "Published desc,Id":
                    searchFilter.SortProperty = SortProperty.Recent;
                    break;
                case "concat(Title,Id),Id":
                    searchFilter.SortProperty = SortProperty.DisplayName;
                    searchFilter.SortDirection = SortDirection.Ascending;
                    break;
                case "concat(Title,Id) desc,Id":
                    searchFilter.SortProperty = SortProperty.DisplayName;
                    searchFilter.SortDirection = SortDirection.Descending;
                    break;
                default:
                    searchFilter.SortProperty = SortProperty.Relevance;
                    break;
            }

            string filterValue = request["$filter"];
            return (filterValue.IndexOf("IsLatestVersion", StringComparison.Ordinal) != -1) ||
                   (filterValue.IndexOf("IsAbsoluteLatestVersion", StringComparison.Ordinal) != -1);
        }

        private int ReadInt(string requestValue, int defaultValue)
        {
            int result;
            return Int32.TryParse(requestValue, out result) ? result : defaultValue;
        }

        protected virtual bool UseHttps()
        {
            return HttpContext.Current.Request.IsSecureConnection;
        }
    }
}
