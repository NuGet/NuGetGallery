using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Services.Providers;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using NuGet.Services.Search.Models;
using QueryInterceptor;

namespace NuGetGallery
{
    public static class SearchAdaptor
    {
        /// <summary>
        ///     Determines the maximum number of packages returned in a single page of an OData result.
        /// </summary>
        internal const int MaxPageSize = 100;

        public static SearchFilter GetSearchFilter(string q, int page, string sortOrder, string context)
        {
            var searchFilter = new SearchFilter(context)
            {
                SearchTerm = q,
                Skip = (page - 1) * Constants.DefaultPackageListPageSize, // pages are 1-based. 
                Take = Constants.DefaultPackageListPageSize,
                IncludePrerelease = true
            };

            switch (sortOrder)
            {
                case Constants.AlphabeticSortOrder:
                    searchFilter.SortOrder = SortOrder.TitleAscending;
                    break;

                case Constants.RecentSortOrder:
                    searchFilter.SortOrder = SortOrder.Published;
                    break;

                default:
                    searchFilter.SortOrder = SortOrder.Relevance;
                    break;
            }

            return searchFilter;
        }

        public static async Task<IQueryable<Package>> GetResultsFromSearchService(ISearchService searchService, SearchFilter searchFilter)
        {
            var result = await searchService.Search(searchFilter);

            // For count queries, we can ask the SearchService to not filter the source results. This would avoid hitting the database and consequently make
            // it very fast.
            if (searchFilter.CountOnly)
            {
                // At this point, we already know what the total count is. We can have it return this value very quickly without doing any SQL.
                return result.Data.InterceptWith(new CountInterceptor(result.Hits));
            }

            // For relevance search, Lucene returns us a paged\sorted list. OData tries to apply default ordering and Take \ Skip on top of this.
            // It also tries to filter to latest versions, but the search service already did that!
            // We avoid it by yanking these expressions out of out the tree.
            return result.Data.InterceptWith(new DisregardODataInterceptor());
        }

        public static async Task<IQueryable<Package>> SearchCore(
            ISearchService searchService,
            HttpRequestBase request,
            IQueryable<Package> packages, 
            string searchTerm, 
            string targetFramework, 
            bool includePrerelease,
            CuratedFeed curatedFeed)
        {
            SearchFilter searchFilter;
            // We can only use Lucene if:
            //  a) We are looking for the latest version of a package OR the Index contains all versions of each package
            //  b) The sort order is something Lucene can handle
            if (TryReadSearchFilter(searchService.ContainsAllVersions, request.RawUrl, out searchFilter))
            {
                searchFilter.SearchTerm = searchTerm;
                searchFilter.IncludePrerelease = includePrerelease;
                searchFilter.CuratedFeed = curatedFeed;
                searchFilter.SupportedFramework = targetFramework;

                var results = await GetResultsFromSearchService(searchService, searchFilter);

                return results;
            }

            if (!includePrerelease)
            {
                packages = packages.Where(p => !p.IsPrerelease);
            }

            return packages.Search(searchTerm);
        }

        private static bool TryReadSearchFilter(bool allVersionsInIndex, string url, out SearchFilter searchFilter)
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

            searchFilter = new SearchFilter(SearchFilter.ODataSearchContext)
            {
                // The way the default paging works is WCF attempts to read up to the MaxPageSize elements. If it finds as many, it'll assume there 
                // are more elements to be paged and generate a continuation link. Consequently we'll always ask to pull MaxPageSize elements so WCF generates the 
                // link for us and then allow it to do a Take on the results. Further down, we'll also parse $skiptoken as a custom IDataServicePagingProvider
                // sneakily injects the Skip value in the continuation token.
                Take = MaxPageSize,
                Skip = 0,
                CountOnly = path.EndsWith("$count", StringComparison.Ordinal)
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
            else if(!allVersionsInIndex)
            {
                searchFilter = null;
                return false;
            }

            string skipStr;
            if (queryTerms.TryGetValue("$skip", out skipStr))
            {
                int skip;
                if (int.TryParse(skipStr, out skip))
                {
                    searchFilter.Skip = skip;
                }
            }

            string topStr;
            if (queryTerms.TryGetValue("$top", out topStr))
            {
                int top;
                if(int.TryParse(topStr, out top))
                {
                    searchFilter.Take = Math.Min(top, MaxPageSize);
                }
            }

            string skipTokenStr;
            if (queryTerms.TryGetValue("$skiptoken", out skipTokenStr))
            {
                var skipTokenParts = skipTokenStr.Split(',');
                if (skipTokenParts.Length == 3) // this means our custom IDataServicePagingProvider did its magic by sneaking the Skip value into the SkipToken
                {
                    int skip;
                    if (int.TryParse(skipTokenParts[2], out skip))
                    {
                        searchFilter.Skip = skip;
                    }
                }
            }

            //  only certain orderBy clauses are supported from the Lucene search
            string orderBy;
            if (queryTerms.TryGetValue("$orderby", out orderBy))
            {
                if (string.IsNullOrEmpty(orderBy))
                {
                    searchFilter.SortOrder = SortOrder.Relevance;
                }
                else if (orderBy.StartsWith("DownloadCount", StringComparison.Ordinal))
                {
                    searchFilter.SortOrder = SortOrder.Relevance;
                }
                else if (orderBy.StartsWith("Published", StringComparison.Ordinal))
                {
                    searchFilter.SortOrder = SortOrder.Published;
                }
                else if (orderBy.StartsWith("LastEdited", StringComparison.Ordinal))
                {
                    searchFilter.SortOrder = SortOrder.LastEdited;
                }
                else if (orderBy.StartsWith("Id", StringComparison.Ordinal))
                {
                    searchFilter.SortOrder = SortOrder.TitleAscending;
                }
                else if (orderBy.StartsWith("concat", StringComparison.Ordinal))
                {
                    searchFilter.SortOrder = SortOrder.TitleAscending;

                    if (orderBy.Contains("%20desc"))
                    {
                        searchFilter.SortOrder = SortOrder.TitleDescending;
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
                searchFilter.SortOrder = SortOrder.Relevance;
            }

            return true;
        }

        public static object GetPagingProvider<TPackage>(ISearchService searchService, HttpRequestBase request)
        {
            // If we delegate to Lucene, we will have to do some paging tricks and inject a custom IDataServicePagingProvider
            SearchFilter searchFilter;
            if (request.Path.Contains("/Search") && TryReadSearchFilter(searchService.ContainsAllVersions, request.RawUrl, out searchFilter))
            {
                return new SearchAdaptorDataServicePagingProvider<TPackage>(searchFilter.Skip);
            }
            return null;
        }

        public class SearchAdaptorDataServicePagingProvider<TPackage> : IDataServicePagingProvider
        {
            private readonly int _currentSkip;
            private object[] _continuationToken;
            private Type _packageType;

            public SearchAdaptorDataServicePagingProvider(int currentSkip)
            {
                _currentSkip = currentSkip;
                _packageType = typeof(TPackage);
            }

            public void SetContinuationToken(IQueryable query, ResourceType resourceType, object[] continuationToken)
            {
                if (resourceType.FullName != _packageType.FullName)
                {
                    throw new ArgumentException("The paging provider can not construct a meaningful continuation token because its type is different from the ResourceType for which a continuation token is requested.");
                }
                
                var materializedQuery = (query as IQueryable<TPackage>).ToList();
                var lastElement = materializedQuery.LastOrDefault();
                if (lastElement != null && materializedQuery.Count == MaxPageSize)
                {
                    string packageId = _packageType.GetProperty("Id").GetValue(lastElement).ToString();
                    string packageVersion = _packageType.GetProperty("Version").GetValue(lastElement).ToString();
                    _continuationToken = new object[]
                    {
                        packageId,
                        packageVersion,
                        _currentSkip + Math.Min(materializedQuery.Count, MaxPageSize)
                    };
                }
                else
                {
                    _continuationToken = null;
                }
            }

            public object[] GetContinuationToken(IEnumerator enumerator)
            {
                return _continuationToken;
            }
        }
    }
}