using System;
using System.Data.Services;
using System.Data.Services.Common;
using System.Data.Services.Providers;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceModel;
using System.Web;
using System.Web.Mvc;
using Microsoft.Data.OData.Query;
using Microsoft.Data.OData.Query.SyntacticAst;
using QueryInterceptor;

namespace NuGetGallery
{
    [ServiceBehavior(IncludeExceptionDetailInFaults = true, ConcurrencyMode = ConcurrencyMode.Multiple)]
    public abstract class FeedServiceBase<TPackage> : DataService<FeedContext<TPackage>>, IDataServiceStreamProvider, IServiceProvider
    {
        /// <summary>
        /// Determines the maximum number of packages returned in a single page of an OData result.
        /// </summary>
        private const int MaxPageSize = 40;
        private readonly IEntitiesContext entities;
        private readonly IEntityRepository<Package> packageRepo;
        private readonly IConfiguration configuration;
        private readonly ISearchService searchService;
        private HttpContextBase httpContext;

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

        protected internal virtual HttpContextBase HttpContext
        {
            get
            {
                return httpContext ?? new HttpContextWrapper(System.Web.HttpContext.Current);
            }
            set
            {
                httpContext = value;
            }
        }

        protected internal string SiteRoot
        {
            get
            {
                string siteRoot = Configuration.GetSiteRoot(UseHttps());
                return EnsureTrailingSlash(siteRoot);
            }
        }

        // This method is called only once to initialize service-wide policies.
        protected static void InitializeServiceBase(DataServiceConfiguration config)
        {
            config.SetServiceOperationAccessRule("Search", ServiceOperationRights.AllRead);
            config.SetServiceOperationAccessRule("FindPackagesById", ServiceOperationRights.AllRead);
            config.SetEntitySetAccessRule("Packages", EntitySetRights.AllRead);
            config.SetEntitySetPageSize("Packages", MaxPageSize);
            config.DataServiceBehavior.MaxProtocolVersion = DataServiceProtocolVersion.V2;
            config.UseVerboseErrors = true;
        }

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

        protected virtual IQueryable<Package> SearchCore(IQueryable<Package> packages, string searchTerm, string targetFramework, bool includePrerelease)
        {
            
            SearchFilter searchFilter;
            // We can only use Lucene if the client queries for the latest versions (IsLatest \ IsLatestStable) versions of a package
            // and specific sort orders that we have in the index.
            if (TryReadSearchFilter(HttpContext.Request, out searchFilter))
            {
                searchFilter.SearchTerm = searchTerm;
                searchFilter.IncludePrerelease = includePrerelease;

                return GetResultsFromSearchService(packages, searchFilter);
            }

            if (!includePrerelease)
            {
                packages = packages.Where(p => !p.IsPrerelease);
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
            var odataQuery = SyntacticTree.ParseUri(new Uri(SiteRoot + request.RawUrl), new Uri(SiteRoot));

            var keywordPath = odataQuery.Path as KeywordSegmentQueryToken;
            searchFilter = new SearchFilter
            {
                // HACK: The way the default paging works is WCF attempts to read up to the MaxPageSize elements. If it finds as many, it'll assume there 
                // are more elements to be paged and generate a continuation link. Consequently we'll always ask to pull MaxPageSize elements so WCF generates the 
                // link for us and then allow it to do a Take on the results. The alternative to do is roll our IDataServicePagingProvider, but we run into 
                // issues since we need to manage state over concurrent requests. This seems like an easier solution.
                Take = MaxPageSize,
                Skip = odataQuery.Skip ?? 0,
                CountOnly = keywordPath != null && keywordPath.Keyword == KeywordKind.Count,
                SortDirection = SortDirection.Ascending
            };

            var filterProperty = odataQuery.Filter as PropertyAccessQueryToken;
            if (filterProperty == null ||
                    !(filterProperty.Name.Equals("IsLatestVersion", StringComparison.Ordinal) ||
                      filterProperty.Name.Equals("IsAbsoluteLatestVersion", StringComparison.Ordinal)))
            {
                // We'll only use the index if we the query searches for latest \ latest-stable packages
                return false;
            }

            var orderBy = odataQuery.OrderByTokens.FirstOrDefault();
            if (orderBy == null || orderBy.Expression == null)
            {
                searchFilter.SortProperty = SortProperty.Relevance;
            }
            else if (orderBy.Expression.Kind == QueryTokenKind.PropertyAccess)
            {
                var propertyAccess = (PropertyAccessQueryToken)orderBy.Expression;
                if (propertyAccess.Name.Equals("DownloadCount", StringComparison.Ordinal))
                {
                    searchFilter.SortProperty = SortProperty.DownloadCount;
                }
                else if (propertyAccess.Name.Equals("Published", StringComparison.Ordinal))
                {
                    searchFilter.SortProperty = SortProperty.Recent;
                }
                else if (propertyAccess.Name.Equals("Id", StringComparison.Ordinal))
                {
                    searchFilter.SortProperty = SortProperty.DisplayName;
                }
                else
                {
                    Debug.WriteLine("Order by clause {0} is unsupported", propertyAccess.Name);
                    return false;
                }
            }
            else if (orderBy.Expression.Kind == QueryTokenKind.FunctionCall)
            {
                var functionCall = (FunctionCallQueryToken)orderBy.Expression;
                if (functionCall.Name.Equals("concat", StringComparison.OrdinalIgnoreCase))
                {
                    // We'll assume this is concat(Title, Id)
                    searchFilter.SortProperty = SortProperty.DisplayName;
                    searchFilter.SortDirection = orderBy.Direction == OrderByDirection.Descending ? SortDirection.Descending : SortDirection.Ascending;
                }
                else
                {
                    Debug.WriteLine("Order by clause {0} is unsupported", functionCall.Name);
                    return false;
                }
            }
            else
            {
                Debug.WriteLine("Order by clause {0} is unsupported", orderBy.Expression.Kind);
                return false;
            }
            return true;
        }

        protected virtual bool UseHttps()
        {
            return HttpContext.Request.IsSecureConnection;
        }

        private static string EnsureTrailingSlash(string siteRoot)
        {
            if (!siteRoot.EndsWith("/", StringComparison.Ordinal))
            {
                siteRoot = siteRoot + '/';
            }
            return siteRoot;
        }
    }
}
