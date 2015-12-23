﻿// Copyright (c) .NET Foundation. All rights reserved.
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
using NuGet.Services.Search.Models;
using NuGetGallery.Infrastructure.Lucene;
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
            return FormatResults(searchFilter, result);
        }

        public static async Task<IQueryable<Package>> GetRawResultsFromSearchService(ISearchService searchService, SearchFilter searchFilter)
        {
            var externalSearchService = searchService as ExternalSearchService;
            if (externalSearchService != null)
            {
                var result = await externalSearchService.RawSearch(searchFilter);
                return FormatResults(searchFilter, result);
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
            return result.Data.InterceptWith(new DisregardODataInterceptor());
        }

        public static async Task<IQueryable<Package>> FindByIdAndVersionCore(
                   ISearchService searchService,
                   HttpRequestBase request,
                   IQueryable<Package> packages,
                   string id,
                   string version,
                   CuratedFeed curatedFeed)
        {
            SearchFilter searchFilter;
            // We can only use Lucene if:
            //  a) We are looking for the latest version of a package OR the Index contains all versions of each package
            //  b) The sort order is something Lucene can handle
            if (TryReadSearchFilter(searchService.ContainsAllVersions, request.RawUrl, out searchFilter))
            {
                var searchTerm = string.Format(CultureInfo.CurrentCulture, "Id:\"{0}\"", id);
                if (!string.IsNullOrEmpty(version))
                {
                    searchTerm = string.Format(CultureInfo.CurrentCulture, "Id:\"{0}\" AND Version:\"{1}\"", id, version);
                }

                searchFilter.SearchTerm = searchTerm;
                searchFilter.IncludePrerelease = true;
                searchFilter.CuratedFeed = curatedFeed;
                searchFilter.SupportedFramework = null;
                searchFilter.IncludeAllVersions = true;

                var results = await GetRawResultsFromSearchService(searchService, searchFilter);

                return results;
            }

            return packages;
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
                    queryTerms[nameValue[0]] = nameValue[1];
                }
            }

            // We'll only use the index if we the query searches for latest / latest-stable packages
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
        
        public static Uri GetNextLink(Uri currentRequestUri, long? totalResultCount, object queryParameters, ODataQueryOptions options, ODataQuerySettings settings)
        {
            if (!totalResultCount.HasValue || totalResultCount.Value <= MaxPageSize || totalResultCount.Value == 0)
            {
                return null; // no need for a next link if there are no additional results
            }
           
            var skipCount = (options.Skip != null ? options.Skip.Value : 0) + Math.Min(totalResultCount.Value, (settings.PageSize != null ? settings.PageSize.Value : SearchAdaptor.MaxPageSize));
            
            var queryBuilder = new StringBuilder();
            
            var queryParametersCollection = new RouteValueDictionary(queryParameters);
            foreach (var queryParameter in queryParametersCollection)
            {
                queryBuilder.Append(Uri.EscapeDataString(queryParameter.Key));
                queryBuilder.Append("=");
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
                queryBuilder.Append("&");
            }

            if (options.SelectExpand != null)
            {
                if (!string.IsNullOrEmpty(options.SelectExpand.RawSelect))
                {
                    queryBuilder.Append("$select=");
                    queryBuilder.Append(options.SelectExpand.RawSelect);
                    queryBuilder.Append("&");
                }
                if (!string.IsNullOrEmpty(options.SelectExpand.RawExpand))
                {
                    queryBuilder.Append("$expand=");
                    queryBuilder.Append(options.SelectExpand.RawExpand);
                    queryBuilder.Append("&");
                }
            }

            if (options.Filter != null)
            {
                queryBuilder.Append("$filter=");
                queryBuilder.Append(options.Filter.RawValue);
                queryBuilder.Append("&");
            }

            if (options.OrderBy != null)
            {
                queryBuilder.Append("$orderby=");
                queryBuilder.Append(options.OrderBy.RawValue);
                queryBuilder.Append("&");
            }

            if (skipCount > 0)
            {
                queryBuilder.Append("$skip=");
                queryBuilder.Append(skipCount);
                queryBuilder.Append("&");
            }

            if (options.Top != null)
            {
                queryBuilder.Append("$top=");
                queryBuilder.Append(options.Top.RawValue);
                queryBuilder.Append("&");
            }

            var queryString = queryBuilder.ToString().TrimEnd('&');

            var builder = new UriBuilder(currentRequestUri);
            builder.Query = queryString;
            return builder.Uri;
        }
    }
}