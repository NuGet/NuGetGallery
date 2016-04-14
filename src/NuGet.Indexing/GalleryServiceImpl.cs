// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Lucene.Net.Search;
using Newtonsoft.Json;

namespace NuGet.Indexing
{
    public class GalleryServiceImpl
    {
        public static void Search(JsonWriter jsonWriter, NuGetSearcherManager searcherManager, string q, bool countOnly, bool includePrerelease, string sortBy, int skip, int take, string feed, bool ignoreFilter, bool luceneQuery)
        {
            if (jsonWriter == null)
            {
                throw new ArgumentNullException(nameof(jsonWriter));
            }

            if (searcherManager == null)
            {
                throw new ArgumentNullException(nameof(searcherManager));
            }

            var searcher = searcherManager.Get();
            try
            {
                // The old V2 search service would treat "id:" queries (~match) in the same way as it did "packageid:" (==match).
                // If "id:" is in the query, replace it.
                if (luceneQuery && !string.IsNullOrEmpty(q) && q.StartsWith("id:", StringComparison.OrdinalIgnoreCase))
                {
                    q = "packageid:" + q.Substring(3);
                }

                // Build the query
                Query query = NuGetQuery.MakeQuery(q, searcher);

                // The behavior below is incorrect when looking at the parameters for this method.
                // One would expect the filters to be applied properly.
                //
                // We are *intentionally* doing it wrong here because our legacy V2 search service ignored
                // these filters as well. The following URL's would yield the exact same result:
                //   /search/query?q=Id:Antlr&prerelease=false&ignoreFilter=false
                //   /search/query?q=Id:Antlr&prerelease=false&ignoreFilter=true
                //   /search/query?q=Id:Antlr&prerelease=true&ignoreFilter=false
                //   /search/query?q=Id:Antlr&prerelease=true&ignoreFilter=true
                //
                // If this behavior needs to be changed, this is the bit of code that will return
                // the data you'd expect looking at the method parameters:
                //   bool includeUnlisted = ignoreFilter;
                //   includePrerelease = ignoreFilter || includePrerelease;
                //   feed = ignoreFilter ? null : feed;
                //   Filter filter = searcher.GetFilter(includeUnlisted, includePrerelease, feed);
                //
                // But hey, we'll do it the old V2 way instead and happily ignore the correct way.

                Filter filter = null;
                if (!ignoreFilter && searcher.TryGetFilter(feed, out filter))
                {
                    // Filter before running the query (make the search set smaller)
                    query = new FilteredQuery(query, filter);
                }
             
                if (countOnly)
                {
                    DocumentCountImpl(jsonWriter, searcher, query);
                }
                else
                {
                    ListDocumentsImpl(jsonWriter, searcher, query, sortBy, skip, take, searcherManager);
                }
            }
            finally
            {
                searcherManager.Release(searcher);
            }
        }

        private static void DocumentCountImpl(JsonWriter jsonWriter, IndexSearcher searcher, Query query)
        {
            TopDocs topDocs = searcher.Search(query, 1);
            ResponseFormatter.WriteV2CountResult(jsonWriter, topDocs.TotalHits);
        }

        private static void ListDocumentsImpl(JsonWriter jsonWriter, NuGetIndexSearcher searcher, Query query, string sortBy, int skip, int take, NuGetSearcherManager manager)
        {
            Query boostedQuery = new RankingScoreQuery(query, searcher.Rankings);

            int nDocs = skip + take;
            Sort sort = GetSort(sortBy);

            TopDocs topDocs = (sort == null)
                ? searcher.Search(boostedQuery, nDocs)
                : searcher.Search(boostedQuery, null, nDocs, sort);

            ResponseFormatter.WriteV2Result(jsonWriter, searcher, topDocs, skip, take);
        }

        private static readonly Dictionary<string, Func<Sort>> _sorts = new Dictionary<string, Func<Sort>>(StringComparer.OrdinalIgnoreCase) {
            {"lastEdited", () => new Sort(new SortField("LastEditedDate", SortField.INT, reverse: true))},
            {"published", () => new Sort(new SortField("PublishedDate", SortField.INT, reverse: true))},
            {"title-asc", () => new Sort(new SortField("SortableTitle", SortField.STRING, reverse: false))},
            {"title-desc", () => new Sort(new SortField("SortableTitle", SortField.STRING, reverse: true))},
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
