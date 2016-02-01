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
        public static void Search(JsonWriter jsonWriter, NuGetSearcherManager searcherManager, string q, bool countOnly, bool includePrerelease, string sortBy, int skip, int take, string feed, bool ignoreFilter)
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
                bool includeUnlisted = ignoreFilter;
                includePrerelease = ignoreFilter || includePrerelease;
                feed = ignoreFilter ? null : feed;
                Filter filter = searcher.GetFilter(includeUnlisted, includePrerelease, feed);

                Query query = NuGetQuery.MakeQuery(q);

                if (countOnly)
                {
                    DocumentCountImpl(jsonWriter, searcher, query, filter);
                }
                else
                {
                    IDictionary<string, int> rankings = searcher.Rankings;

                    ListDocumentsImpl(jsonWriter, searcher, query, rankings, filter, sortBy, skip, take, searcherManager);
                }
            }
            finally
            {
                searcherManager.Release(searcher);
            }
        }

        private static void DocumentCountImpl(JsonWriter jsonWriter, IndexSearcher searcher, Query query, Filter filter)
        {
            TopDocs topDocs = searcher.Search(query, filter, 1);
            ResponseFormatter.WriteV2CountResult(jsonWriter, topDocs.TotalHits);
        }

        private static void ListDocumentsImpl(JsonWriter jsonWriter, NuGetIndexSearcher searcher, Query query, IDictionary<string, int> rankings, Filter filter, string sortBy, int skip, int take, NuGetSearcherManager manager)
        {
            Query boostedQuery = new RankingScoreQuery(query, rankings);

            int nDocs = skip + take;
            Sort sort = GetSort(sortBy);

            TopDocs topDocs = (sort == null) ?
                searcher.Search(boostedQuery, filter, nDocs) :
                searcher.Search(boostedQuery, filter, nDocs, sort);

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
