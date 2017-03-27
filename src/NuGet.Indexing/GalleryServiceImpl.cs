// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Lucene.Net.Search;
using Newtonsoft.Json;
using NuGet.Versioning;

namespace NuGet.Indexing
{
    public class GalleryServiceImpl
    {
        public static void Search(JsonWriter jsonWriter, 
            NuGetSearcherManager searcherManager, 
            string q, 
            bool countOnly, 
            bool includePrerelease, 
            NuGetVersion semVerLevel, 
            string sortBy, 
            int skip, 
            int take, 
            string feed, 
            bool ignoreFilter, 
            bool luceneQuery)
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
                Query query = NuGetQuery.MakeQuery(q, searcher.Owners);

                // Build filter
                bool includeUnlisted = ignoreFilter;
                includePrerelease = ignoreFilter || includePrerelease;
                feed = ignoreFilter ? null : feed;

                Filter filter = null;
                if (!ignoreFilter && searcher.TryGetFilter(includeUnlisted, includePrerelease, semVerLevel, feed, out filter))
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
                    ListDocumentsImpl(jsonWriter, searcher, query, sortBy, skip, take, semVerLevel);
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

        private static void ListDocumentsImpl(JsonWriter jsonWriter,
            NuGetIndexSearcher searcher,
            Query query,
            string sortBy,
            int skip,
            int take,
            NuGetVersion semVerLevel)
        {
            Query boostedQuery = new DownloadsBoostedQuery(query,
                searcher.DocIdMapping,
                searcher.Downloads,
                searcher.Rankings,
                searcher.QueryBoostingContext);

            int nDocs = skip + take;
            Sort sort = GetSort(sortBy);

            TopDocs topDocs = (sort == null)
                ? searcher.Search(boostedQuery, nDocs)
                : searcher.Search(boostedQuery, null, nDocs, sort);

            ResponseFormatter.WriteV2Result(jsonWriter, searcher, topDocs, skip, take, semVerLevel);
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
