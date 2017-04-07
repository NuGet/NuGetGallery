// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Net;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers;
using Lucene.Net.Search;
using Newtonsoft.Json;
using NuGet.Versioning;

namespace NuGet.Indexing
{
    public static class ServiceImpl
    {
        public static void Search(JsonWriter jsonWriter,
            NuGetSearcherManager searcherManager,
            string scheme,
            string q,
            bool includePrerelease,
            NuGetVersion semVerLevel,
            int skip,
            int take,
            string feed,
            bool includeExplanation)
        {
            var searcher = searcherManager.Get();

            try
            {
                Query query = MakeSearchQuery(q, searcher);

                Filter filter = null;

                if (searcher.TryGetFilter(false, includePrerelease, semVerLevel, feed, out filter))
                {
                    // Filter before running the query (make the search set smaller)
                    query = new FilteredQuery(query, filter);
                }

                TopDocs topDocs = searcher.Search(query, skip + take);

                ResponseFormatter.WriteSearchResult(jsonWriter, searcher, scheme, topDocs, skip, take, includePrerelease, includeExplanation, semVerLevel, query);
            }
            finally
            {
                searcherManager.Release(searcher);
            }
        }

        public static void AutoComplete(
            JsonWriter jsonWriter,
            NuGetSearcherManager searcherManager,
            string q,
            string id,
            bool includePrerelease,
            NuGetVersion semVerLevel,
            int skip,
            int take,
            bool includeExplanation)
        {
            var searcher = searcherManager.Get();
            try
            {
                Filter filter = null;

                if (q != null)
                {
                    Query query = MakeAutoCompleteQuery(q,
                        searcher.DocIdMapping,
                        searcher.Downloads,
                        searcher.Rankings,
                        searcher.QueryBoostingContext);

                    if (searcher.TryGetFilter(false, includePrerelease, semVerLevel, null, out filter))
                    {
                        // Filter before running the query (make the search set smaller)
                        query = new FilteredQuery(query, filter);
                    }

                    TopDocs topDocs = searcher.Search(query, skip + take);
                    ResponseFormatter.WriteAutoCompleteResult(jsonWriter, searcher, topDocs, skip, take, includeExplanation, query);
                }
                else
                {
                    Query query = MakeAutoCompleteVersionQuery(id);

                    if (searcher.TryGetFilter(false, includePrerelease, semVerLevel, null, out filter))
                    {
                        // Filter before running the query (make the search set smaller)
                        query = new FilteredQuery(query, filter);
                    }

                    TopDocs topDocs = searcher.Search(query, 1);
                    ResponseFormatter.WriteAutoCompleteVersionResult(jsonWriter, searcher, includePrerelease, semVerLevel, topDocs);
                }
            }
            finally
            {
                searcherManager.Release(searcher);
            }
        }

        private static Query MakeSearchQuery(string q, NuGetIndexSearcher searcher)
        {
            try
            {
                Query query = NuGetQuery.MakeQuery(q, searcher.Owners);
                Query boostedQuery = new DownloadsBoostedQuery(query,
                    searcher.DocIdMapping,
                    searcher.Downloads,
                    searcher.Rankings,
                    searcher.QueryBoostingContext);

                return boostedQuery;
            }
            catch (ParseException)
            {
                throw new ClientException(HttpStatusCode.BadRequest, "Invalid query format");
            }
        }

        private static Query MakeAutoCompleteQuery(string q,
            IReadOnlyDictionary<string, int[]> docIdMapping,
            Downloads downloads,
            RankingResult rankings,
            QueryBoostingContext context)
        {
            var query = NuGetQuery.MakeAutoCompleteQuery(q);

            var boostedQuery = new DownloadsBoostedQuery(query,
                docIdMapping,
                downloads,
                rankings,
                context,
                2.0);

            return boostedQuery;
        }

        private static Query MakeAutoCompleteVersionQuery(string id)
        {
            Query query = new TermQuery(new Term("Id", id.ToLowerInvariant()));
            return query;
        }

        private static Query MakeFindQuery(string id)
        {
            string analyzedId = id.ToLowerInvariant();
            Query query = new TermQuery(new Term("Id", analyzedId));
            return query;
        }
    }
}