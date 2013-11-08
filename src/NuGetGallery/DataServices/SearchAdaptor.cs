using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Web;
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
            IQueryable<Package> packages, 
            string searchTerm, 
            string targetFramework, 
            bool includePrerelease,
            int? curatedFeedKey)
        {
            SearchFilter searchFilter;
            // We can only use Lucene if the client queries for the latest versions (IsLatest \ IsLatestStable) versions of a package
            // and specific sort orders that we have in the index.
            if (TryReadSearchFilter(request.RawUrl, out searchFilter))
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

        private static bool TryReadSearchFilter(string url, out SearchFilter searchFilter)
        {
            if (url == null)
            {
                searchFilter = null;
                return false;
            }

            int indexOfQuestionMark = url.IndexOf('?');

            if (indexOfQuestionMark == -1)
            {
                searchFilter = null;
                return false;
            }

            string path = url.Substring(0, indexOfQuestionMark);
            string query = url.Substring(indexOfQuestionMark + 1);

            if (string.IsNullOrEmpty(query))
            {
                searchFilter = null;
                return false;
            }

            searchFilter = new SearchFilter
            {
                // The way the default paging works is WCF attempts to read up to the MaxPageSize elements. If it finds as many, it'll assume there 
                // are more elements to be paged and generate a continuation link. Consequently we'll always ask to pull MaxPageSize elements so WCF generates the 
                // link for us and then allow it to do a Take on the results. The alternative to do is roll our IDataServicePagingProvider, but we run into 
                // issues since we need to manage state over concurrent requests. This seems like an easier solution.
                Take = MaxPageSize,
                Skip = 0,
                CountOnly = path.EndsWith("$count", StringComparison.Ordinal),
                SortDirection = SortDirection.Ascending
            };

            string[] props = query.Split('&');

            IDictionary<string, string> queryTerms = new Dictionary<string, string>();
            foreach (string prop in props)
            {
                string[] nameValue = prop.Split('=');
                if (nameValue.Length == 2)
                {
                    queryTerms[nameValue[0]] = nameValue[1];
                }
            }

            // We'll only use the index if we the query searches for latest \ latest-stable packages

            string filter;
            if (queryTerms.TryGetValue("$filter", out filter))
            {
                if (!(filter.Equals("IsLatestVersion", StringComparison.Ordinal) || filter.Equals("IsAbsoluteLatestVersion", StringComparison.Ordinal)))
                {
                    searchFilter = null;
                    return false;
                }
            }
            else
            {
                searchFilter = null;
                return false;
            }

            string skip;
            if (queryTerms.TryGetValue("$skip", out skip))
            {
                int result;
                if (int.TryParse(skip, out result))
                {
                    searchFilter.Skip = result;
                }
            }

            //  only certain orderBy clauses are supported from the Lucene search

            string orderBy;
            if (queryTerms.TryGetValue("$orderby", out orderBy))
            {
                if (string.IsNullOrEmpty(orderBy))
                {
                    searchFilter.SortProperty = SortProperty.Relevance;
                }
                else if (orderBy.StartsWith("DownloadCount", StringComparison.Ordinal))
                {
                    searchFilter.SortProperty = SortProperty.DownloadCount;
                }
                else if (orderBy.StartsWith("Published", StringComparison.Ordinal))
                {
                    searchFilter.SortProperty = SortProperty.Recent;
                }
                else if (orderBy.StartsWith("LastEdited", StringComparison.Ordinal))
                {
                    searchFilter.SortProperty = SortProperty.RecentlyEdited;
                }
                else if (orderBy.StartsWith("Id", StringComparison.Ordinal))
                {
                    searchFilter.SortProperty = SortProperty.DisplayName;
                }
                else if (orderBy.StartsWith("concat", StringComparison.Ordinal))
                {
                    searchFilter.SortProperty = SortProperty.DisplayName;

                    if (orderBy.Contains("%20desc"))
                    {
                        searchFilter.SortDirection = SortDirection.Descending;
                    }
                }
                else
                {
                    searchFilter = null;
                    return false;
                }
            }
            else
            {
                searchFilter.SortProperty = SortProperty.Relevance;
            }

            return true;
        }
    }
}