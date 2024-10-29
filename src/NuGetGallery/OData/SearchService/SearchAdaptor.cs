// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http.OData.Query;
using System.Web.Routing;
using NuGet.Services.Entities;
using NuGetGallery.Infrastructure.Search;
using NuGetGallery.Infrastructure.Search.Models;
using NuGetGallery.OData.QueryFilter;
using NuGetGallery.OData.QueryInterceptors;
using QueryInterceptor;

namespace NuGetGallery.OData
{
    public static class SearchAdaptor
    {
        /// <summary>
        ///     Determines the maximum number of packages returned in a single page of an OData result.
        /// </summary>
        internal const int MaxPageSize = 100;
        private static readonly IReadOnlyDictionary<string, SortOrder> SortOrders = new Dictionary<string, SortOrder>(StringComparer.OrdinalIgnoreCase)
        {
            { GalleryConstants.AlphabeticSortOrder, SortOrder.TitleAscending },
            { GalleryConstants.SearchSortNames.TitleAsc, SortOrder.TitleAscending },
            { GalleryConstants.SearchSortNames.TitleDesc, SortOrder.TitleDescending },
            { GalleryConstants.RecentSortOrder, SortOrder.Published },
            { GalleryConstants.SearchSortNames.Published, SortOrder.Published },
            { GalleryConstants.SearchSortNames.LastEdited, SortOrder.LastEdited },
            { GalleryConstants.SearchSortNames.CreatedAsc, SortOrder.CreatedAscending },
            { GalleryConstants.SearchSortNames.CreatedDesc, SortOrder.CreatedDescending },
            { GalleryConstants.SearchSortNames.TotalDownloadsAsc, SortOrder.TotalDownloadsAscending },
            { GalleryConstants.SearchSortNames.TotalDownloadsDesc, SortOrder.TotalDownloadsDescending },
        };

        public static SearchFilter GetSearchFilter(
            string q,
            int page,
            bool includePrerelease,
            string frameworks,
            string tfms,
            bool includeComputedFrameworks,
            string frameworkFilterMode,
            string packageType,
            string sortOrder,
            string context,
            string semVerLevel,
            bool includeTestData)
        {
            page = page < 1 ? 1 : page; // pages are 1-based. 
            frameworks = frameworks ?? string.Empty;
            tfms = tfms ?? string.Empty;
            frameworkFilterMode = frameworkFilterMode ?? "all";
            packageType = packageType ?? string.Empty;

            var searchFilter = new SearchFilter(context)
            {
                SearchTerm = q,
                Skip = (page - 1) * GalleryConstants.DefaultPackageListPageSize,
                Take = GalleryConstants.DefaultPackageListPageSize,
                IncludePrerelease = includePrerelease,
                SemVerLevel = semVerLevel,
                Frameworks = frameworks,
                Tfms = tfms,
                IncludeComputedFrameworks = includeComputedFrameworks,
                FrameworkFilterMode = frameworkFilterMode,
                PackageType = packageType,
                IncludeTestData = includeTestData,
            };

            if (sortOrder == null || !SortOrders.TryGetValue(sortOrder, out var sortOrderValue))
            {
                sortOrderValue = SortOrder.Relevance;
            }

            searchFilter.SortOrder = sortOrderValue;

            return searchFilter;
        }

        private static async Task<SearchResults> GetResultsFromSearchService(ISearchService searchService, SearchFilter searchFilter)
        {
            return await searchService.Search(searchFilter);
        }

        private static async Task<SearchResults> GetRawResultsFromSearchService(ISearchService searchService, SearchFilter searchFilter)
        {
            var externalSearchService = searchService as ExternalSearchService;
            if (externalSearchService != null)
            {
                return await externalSearchService.RawSearch(searchFilter);
            }

            return await GetResultsFromSearchService(searchService, searchFilter);
        }

        private static IQueryable<Package> FormatResults(SearchFilter searchFilter, SearchResults result)
        {
            // For count queries, we can ask the SearchService to not filter the source results. This would avoid hitting the database and consequently make it very fast.
            if (searchFilter.CountOnly)
            {
                // At this point, we already know what the total count is. We can have it return this value very quickly without doing any SQL.
                return result.Data.InterceptWith(new CountInterceptor(result.Hits));
            }

            // For relevance search, Lucene returns us a paged/sorted list. OData tries to apply default ordering and Take / Skip on top of this.
            // It also tries to filter to latest versions, but the search service already did that!
            // We avoid it by yanking these expressions out of out the tree.
            return result.Data
                .InterceptWith(new CountInterceptor(result.Hits))
                .InterceptWith(new DisregardODataInterceptor());
        }

        public static async Task<SearchAdaptorResult> FindByIdAndVersionCore(
                   ISearchService searchService,
                   HttpRequestBase request,
                   IQueryable<Package> packages,
                   string id,
                   string version,
                   string semVerLevel)
        {
            SearchFilter searchFilter;
            // We can only use Lucene if:
            //  a) The Index contains all versions of each package
            //  b) The sort order is something Lucene can handle
            if (TryReadSearchFilter(
                searchService.ContainsAllVersions,
                request.RawUrl,
                searchService.ContainsAllVersions,
                SortOrder.CreatedAscending,
                out searchFilter) && !string.IsNullOrWhiteSpace(id))
            {
                var normalizedRegistrationId = id.Normalize(NormalizationForm.FormC);

                var searchTerm = string.Format(CultureInfo.CurrentCulture, "Id:\"{0}\"", normalizedRegistrationId);
                if (!string.IsNullOrEmpty(version))
                {
                    searchTerm = string.Format(CultureInfo.CurrentCulture, "Id:\"{0}\" AND Version:\"{1}\"", normalizedRegistrationId, version);

                    searchFilter.Take = 1; // only one result is needed in this case
                }

                searchFilter.SearchTerm = searchTerm;
                searchFilter.SemVerLevel = semVerLevel;
                searchFilter.IncludePrerelease = true;
                searchFilter.SupportedFramework = null;
                searchFilter.IncludeAllVersions = true;

                var results = await GetRawResultsFromSearchService(searchService, searchFilter);

                if (SearchResults.IsSuccessful(results))
                {
                    return new SearchAdaptorResult(true, FormatResults(searchFilter, results));
                }
            }

            return new SearchAdaptorResult(false, packages);
        }

        public static async Task<SearchAdaptorResult> SearchCore(
            ISearchService searchService,
            HttpRequestBase request,
            IQueryable<Package> packages, 
            string searchTerm, 
            string targetFramework, 
            bool includePrerelease,
            string semVerLevel)
        {
            SearchFilter searchFilter;
            // We can only use Lucene if:
            //  a) We are looking for the latest version of a package OR the Index contains all versions of each package
            //  b) The sort order is something Lucene can handle
            if (TryReadSearchFilter(
                searchService.ContainsAllVersions,
                request.RawUrl,
                ignoreLatestVersionFilter: false,
                defaultSortOrder: SortOrder.Relevance,
                searchFilter: out searchFilter))
            {
                searchFilter.SearchTerm = searchTerm;
                searchFilter.IncludePrerelease = includePrerelease;
                searchFilter.SupportedFramework = targetFramework;
                searchFilter.SemVerLevel = semVerLevel;

                var results = await GetResultsFromSearchService(searchService, searchFilter);

                if (SearchResults.IsSuccessful(results))
                {
                    return new SearchAdaptorResult(true, FormatResults(searchFilter, results));
                }
            }

            if (!includePrerelease)
            {
                packages = packages.Where(p => !p.IsPrerelease);
            }

            packages = packages.Where(SemVerLevelKey.IsPackageCompliantWithSemVerLevelPredicate(semVerLevel));
    
            return new SearchAdaptorResult(false, packages.Search(searchTerm));
        }

        private static bool TryReadSearchFilter(
            bool allVersionsInIndex,
            string url,
            bool ignoreLatestVersionFilter,
            SortOrder defaultSortOrder,
            out SearchFilter searchFilter)
        {
            if (url == null)
            {
                searchFilter = null;
                return false;
            }

            string path = string.Empty;
            string query = string.Empty;
            int indexOfQuestionMark = url.IndexOf('?');
            if (indexOfQuestionMark > -1)
            {
                path = url.Substring(0, indexOfQuestionMark);
                query = url.Substring(indexOfQuestionMark + 1);
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
                    queryTerms[Uri.UnescapeDataString(nameValue[0])] = nameValue[1];
                }
            }

            // We'll only use the index if we the query searches for latest / latest-stable packages *or* the index contains all versions
            string filter;
            if (queryTerms.TryGetValue("$filter", out filter))
            {
                if (!ignoreLatestVersionFilter 
                    && !(filter.Equals(ODataQueryFilter.IsLatestVersion, StringComparison.Ordinal) 
                        || filter.Equals(ODataQueryFilter.IsAbsoluteLatestVersion, StringComparison.Ordinal)))
                {
                    searchFilter = null;
                    return false;
                }
            }
            else if (!allVersionsInIndex)
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
                if (int.TryParse(topStr, out top))
                {
                    searchFilter.Take = Math.Max(top, MaxPageSize);
                }
            }

            //  only certain orderBy clauses are supported from the Lucene search
            string orderBy;
            if (queryTerms.TryGetValue("$orderby", out orderBy))
            {
                if (string.IsNullOrEmpty(orderBy))
                {
                    searchFilter.SortOrder = defaultSortOrder;
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
                else if (orderBy.StartsWith("Created", StringComparison.Ordinal))
                {
                    searchFilter.SortOrder = SortOrder.CreatedAscending;

                    if (orderBy.Contains("%20desc"))
                    {
                        searchFilter.SortOrder = SortOrder.CreatedDescending;
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
                searchFilter.SortOrder = defaultSortOrder;
            }

            return true;
        }
        
        public static Uri GetNextLink(Uri currentRequestUri, long? totalResultCount, object queryParameters, ODataQueryOptions options, ODataQuerySettings settings, int? semVerLevelKey = null)
        {
            if (!totalResultCount.HasValue || totalResultCount.Value <= MaxPageSize || totalResultCount.Value == 0)
            {
                return null; // no need for a next link if there are no additional results on this page
            }
           
            var skipCount = (options.Skip != null ? options.Skip.Value : 0) + Math.Min(totalResultCount.Value, (settings.PageSize != null ? settings.PageSize.Value : SearchAdaptor.MaxPageSize));

            if (totalResultCount.Value <= skipCount)
            {
                return null; // no need for a next link if there are no additional results in the result set
            }

            var queryBuilder = new StringBuilder();
            
            var queryParametersCollection = new RouteValueDictionary(queryParameters);
            foreach (var queryParameter in queryParametersCollection)
            {
                queryBuilder.Append(Uri.EscapeDataString(queryParameter.Key));
                queryBuilder.Append('=');
                if (queryParameter.Value != null)
                {
                    if (queryParameter.Value is string)
                    {
                        queryBuilder.Append(Uri.EscapeDataString("'" + queryParameter.Value + "'"));
                    }
                    else if (queryParameter.Value is bool)
                    {
                        queryBuilder.Append(queryParameter.Value.ToString().ToLowerInvariant());
                    }
                    else
                    {
                        queryBuilder.Append(queryParameter.Value.ToString().ToLowerInvariant());
                    }
                }
                queryBuilder.Append('&');
            }

            if (options.SelectExpand != null)
            {
                if (!string.IsNullOrEmpty(options.SelectExpand.RawSelect))
                {
                    queryBuilder.Append("$select=");
                    queryBuilder.Append(options.SelectExpand.RawSelect);
                    queryBuilder.Append('&');
                }
                if (!string.IsNullOrEmpty(options.SelectExpand.RawExpand))
                {
                    queryBuilder.Append("$expand=");
                    queryBuilder.Append(options.SelectExpand.RawExpand);
                    queryBuilder.Append('&');
                }
            }

            if (options.Filter != null)
            {
                queryBuilder.Append("$filter=");
                queryBuilder.Append(options.Filter.RawValue);
                queryBuilder.Append('&');
            }

            if (options.OrderBy != null)
            {
                queryBuilder.Append("$orderby=");
                queryBuilder.Append(options.OrderBy.RawValue);
                queryBuilder.Append('&');
            }

            if (skipCount > 0)
            {
                queryBuilder.Append("$skip=");
                queryBuilder.Append(skipCount);
                queryBuilder.Append('&');
            }

            if (options.Top != null)
            {
                queryBuilder.Append("$top=");
                queryBuilder.Append(options.Top.RawValue);
                queryBuilder.Append('&');
            }

            if (semVerLevelKey != null)
            {
                if(semVerLevelKey == SemVerLevelKey.SemVer2)
                {
                    queryBuilder.Append("semVerLevel=");
                    queryBuilder.Append(SemVerLevelKey.SemVerLevel2);
                    queryBuilder.Append('&');
                }
            }

            var queryString = queryBuilder.ToString().TrimEnd('&');

            var builder = new UriBuilder(currentRequestUri);
            builder.Query = queryString;
            return builder.Uri;
        }
    }
}