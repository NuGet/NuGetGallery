// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Lucene.Net.Search;
using Microsoft.Owin;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace NuGet.Indexing
{
    public class GalleryServiceImpl
    {
        public static string Query(IOwinContext context, NuGetSearcherManager searcherManager)
        {
            Trace.TraceInformation("Search: {0}", context.Request.QueryString);

            string q = context.Request.Query["q"] ?? string.Empty;

            string sortBy = context.Request.Query["sortBy"] ?? string.Empty;

            bool includePrerelease;
            if (!bool.TryParse(context.Request.Query["prerelease"], out includePrerelease))
            {
                includePrerelease = false;
            }

            bool countOnly;
            if (!bool.TryParse(context.Request.Query["countOnly"], out countOnly))
            {
                countOnly = false;
            }

            string feed = context.Request.Query["feed"];

            int skip;
            if (!int.TryParse(context.Request.Query["skip"], out skip))
            {
                skip = 0;
            }

            int take;
            if (!int.TryParse(context.Request.Query["take"], out take))
            {
                take = 20;
            }

            //  currently not used 
            //string projectType = context.Request.Query["projectType"] ?? string.Empty;
            //string supportedFramework = context.Request.Query["supportedFramework"];

            return QuerySearch(searcherManager, q, countOnly, includePrerelease, sortBy, skip, take, feed);
        }

        public static string QuerySearch(NuGetSearcherManager searcherManager, string q, bool countOnly, bool includePrerelease, string sortBy, int skip, int take, string feed)
        {
            var searcher = searcherManager.Get();
            try
            {
                Filter filter = searcher.GetFilter(false, includePrerelease, feed);

                Query query = LuceneQueryCreator.Parse(q, false);

                if (countOnly)
                {
                    return DocumentCountImpl(searcher, query, filter);
                }
                else
                {
                    IDictionary<string, int> rankings = searcher.Rankings;

                    return ListDocumentsImpl(searcher, query, rankings, filter, sortBy, skip, take, searcherManager);
                }
            }
            finally
            {
                searcherManager.Release(searcher);
            }
        }

        public static Query MakeQuery(string q, IDictionary<string, int> rankings)
        {
            Query query = LuceneQueryCreator.Parse(q, false);
            Query boostedQuery = new RankingScoreQuery(query, rankings);
            return boostedQuery;
        }

        private static string DocumentCountImpl(IndexSearcher searcher, Query query, Filter filter)
        {
            TopDocs topDocs = searcher.Search(query, filter, 1);
            return ResponseFormatter.MakeCountResultV2(topDocs.TotalHits);
        }

        private static string ListDocumentsImpl(NuGetIndexSearcher searcher, Query query, IDictionary<string, int> rankings, Filter filter, string sortBy, int skip, int take, NuGetSearcherManager manager)
        {
            Query boostedQuery = new RankingScoreQuery(query, rankings);

            int nDocs = skip + take;
            Sort sort = GetSort(sortBy);

            TopDocs topDocs = (sort == null) ?
                searcher.Search(boostedQuery, filter, nDocs) :
                searcher.Search(boostedQuery, filter, nDocs, sort);

            return ResponseFormatter.MakeResultsV2(searcher, topDocs, skip, take);
        }

        private static readonly Dictionary<string, Func<Sort>> _sorts = new Dictionary<string, Func<Sort>>(StringComparer.OrdinalIgnoreCase) {
            {"lastEdited", () => new Sort(new SortField("EditedDate", SortField.INT, reverse: true))},
            {"published", () => new Sort(new SortField("PublishedDate", SortField.INT, reverse: true))},
            {"title-asc", () => new Sort(new SortField("DisplayName", SortField.STRING, reverse: false))},
            {"title-desc", () => new Sort(new SortField("DisplayName", SortField.STRING, reverse: true))},
        };

        private static Sort GetSort(string sortBy)
        {
            if (String.IsNullOrEmpty(sortBy))
            {
                return null;
            }
            Func<Sort> sort;
            if (!_sorts.TryGetValue(sortBy, out sort))
            {
                return null;
            }
            return sort();
        }
    }
}
