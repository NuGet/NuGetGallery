using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Web;
using Microsoft.Data.OData.Query;
using Microsoft.Data.OData.Query.SyntacticAst;
using QueryInterceptor;

namespace NuGetGallery
{
    public static class SearchAdaptor
    {
        /// <summary>
        ///     Determines the maximum number of packages returned in a single page of an OData result.
        /// </summary>
        internal const int MaxPageSize = 40;

        public static SearchFilter GetSearchFilter(string q, string sortOrder, int page, bool includePrerelease)
        {
            var searchFilter = new SearchFilter
            {
                SearchTerm = q,
                Skip = (page - 1) * Constants.DefaultPackageListPageSize, // pages are 1-based. 
                Take = Constants.DefaultPackageListPageSize,
                IncludePrerelease = includePrerelease
            };

            switch (sortOrder)
            {
                case Constants.AlphabeticSortOrder:
                    searchFilter.SortProperty = SortProperty.DisplayName;
                    searchFilter.SortDirection = SortDirection.Ascending;
                    break;

                case Constants.RecentSortOrder:
                    searchFilter.SortProperty = SortProperty.Recent;
                    break;

                case Constants.PopularitySortOrder:
                    searchFilter.SortProperty = SortProperty.DownloadCount;
                    break;

                default:
                    searchFilter.SortProperty = SortProperty.Relevance;
                    break;
            }

            return searchFilter;
        }

        public static IQueryable<Package> GetResultsFromSearchService(ISearchService searchService, SearchFilter searchFilter)
        {
            int totalHits;
            var result = searchService.Search(searchFilter, out totalHits);

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

        public static IQueryable<Package> SearchCore(
            ISearchService searchService,
            HttpRequestBase request,
            string siteRoot,
            IQueryable<Package> packages, 
            string searchTerm, 
            string targetFramework, 
            bool includePrerelease,
            int? curatedFeedKey)
        {
            SearchFilter searchFilter;
            // We can only use Lucene if the client queries for the latest versions (IsLatest \ IsLatestStable) versions of a package
            // and specific sort orders that we have in the index.
            if (TryReadSearchFilter(request, siteRoot, out searchFilter))
            {
                searchFilter.SearchTerm = searchTerm;
                searchFilter.IncludePrerelease = includePrerelease;
                searchFilter.CuratedFeedKey = curatedFeedKey;

                Trace.WriteLine("TODO: use target framework parameter - see #856" + targetFramework);

                var results = GetResultsFromSearchService(searchService, searchFilter);

                return results;
            }

            if (!includePrerelease)
            {
                packages = packages.Where(p => !p.IsPrerelease);
            }

            return packages.Search(searchTerm);
        }

        private static bool TryReadSearchFilter(HttpRequestBase request, string siteRoot, out SearchFilter searchFilter)
        {
            var odataQuery = SyntacticTree.ParseUri(new Uri(siteRoot + request.RawUrl), new Uri(siteRoot));

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
    }
}