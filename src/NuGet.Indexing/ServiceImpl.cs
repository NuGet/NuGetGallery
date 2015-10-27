// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Lucene.Net.Index;
using Lucene.Net.QueryParsers;
using Lucene.Net.Search;
using Microsoft.Owin;
using System;
using System.Collections.Generic;
using System.Net;

namespace NuGet.Indexing
{
    public static class ServiceImpl
    {
        public static string Query(IOwinContext context, NuGetSearcherManager searcherManager)
        {
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

            bool countOnly;
            if (!bool.TryParse(context.Request.Query["countOnly"], out countOnly))
            {
                countOnly = false;
            }

            bool includePrerelease;
            if (!bool.TryParse(context.Request.Query["prerelease"], out includePrerelease))
            {
                includePrerelease = false;
            }

            string feed = context.Request.Query["feed"];

            bool includeExplanation;
            if (!bool.TryParse(context.Request.Query["explanation"], out includeExplanation))
            {
                includeExplanation = false;
            }

            //  currently not used 
            //string projectType = context.Request.Query["projectType"] ?? string.Empty;
            //string supportedFramework = context.Request.Query["supportedFramework"];

            string q = context.Request.Query["q"] ?? string.Empty;
            ValidateQuery(q);

            string scheme = context.Request.Uri.Scheme;

            return QuerySearch(searcherManager, scheme, q, countOnly, includePrerelease, skip, take, feed, includeExplanation);
        }

        public static void ValidateQuery(string query)
        {
            string details = string.Empty;

            // " need to be shown as pair
            if ( query.Split('\"').Length % 2 == 0 )
            {
                throw new ClientException(HttpStatusCode.BadRequest, "Invalid query format: " + query + " ");
            }
        }

        public static string QuerySearch(NuGetSearcherManager searcherManager, string scheme, string q, bool countOnly, bool includePrerelease, int skip, int take, string feed, bool includeExplanation)
        {
            var searcher = searcherManager.Get();
            try
            {
                Query query = MakeQuery(q, searcher.Rankings);
                TopDocs topDocs;

                Filter filter = searcher.GetFilter(false, includePrerelease, feed);

                //TODO: uncomment these lines when we have an index that contains the appropriate @type field in every document
                //Filter typeFilter = new CachingWrapperFilter(new TypeFilter("http://schema.nuget.org/schema#NuGetClassicPackage"));
                //filter = new ChainedFilter(new Filter[] { filter, typeFilter }, ChainedFilter.Logic.AND);

                topDocs = searcher.Search(query, filter, skip + take);

                return ResponseFormatter.MakeResult(searcher, scheme, topDocs, skip, take, includePrerelease, includeExplanation, query);
            }
            finally
            {
                searcherManager.Release(searcher);
            }
        }

        public static Query MakeQuery(string q, IDictionary<string, int> rankings)
        {
            try
            {
                Query query = LuceneQueryCreator.Parse(q, false);
                Query boostedQuery = new RankingScoreQuery(query, rankings);
                return boostedQuery;
            }
            catch (ParseException)
            {
                throw new ClientException(HttpStatusCode.BadRequest, "Invalid query format");
            }
        }

        public static string AutoComplete(IOwinContext context, NuGetSearcherManager searcherManager)
        {
            var searcher = searcherManager.Get();
            try
            {
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

                bool includePrerelease;
                if (!bool.TryParse(context.Request.Query["prerelease"], out includePrerelease))
                {
                    includePrerelease = false;
                }

                bool includeExplanation;
                if (!bool.TryParse(context.Request.Query["explanation"], out includeExplanation))
                {
                    includeExplanation = false;
                }

                string q = context.Request.Query["q"]; 
                string id = context.Request.Query["id"];

                if (q == null && id == null)
                {
                    q = string.Empty;
                }

                return AutoCompleteSearch(searcherManager, q, id, includePrerelease, skip, take, includeExplanation);
            }
            finally
            {
                searcherManager.Release(searcher);
            }
        }

        static string AutoCompleteSearch(NuGetSearcherManager searcherManager, string q, string id, bool includePrerelease, int skip, int take, bool includeExplanation)
        {
            var searcher = searcherManager.Get();
            try
            {
                Filter filter = searcher.GetFilter(false, includePrerelease, null);

                if (q != null)
                {
                    Query query = AutoCompleteMakeQuery(q, searcher.Rankings);
                    TopDocs topDocs = searcher.Search(query, filter, skip + take);
                    return ResponseFormatter.AutoCompleteMakeResult(searcher, topDocs, skip, take, includeExplanation, query);
                }
                else
                {
                    Query query = AutoCompleteVersionMakeQuery(id);
                    TopDocs topDocs = searcher.Search(query, filter, 1);
                    return ResponseFormatter.AutoCompleteMakeVersionResult(searcher, includePrerelease, topDocs);
                }
            }
            finally
            {
                searcherManager.Release(searcher);
            }
        }

        static Query AutoCompleteMakeQuery(string q, IDictionary<string, int> rankings)
        {
            if (string.IsNullOrEmpty(q))
            {
                return new MatchAllDocsQuery();
            }

            QueryParser queryParser = new QueryParser(Lucene.Net.Util.Version.LUCENE_30, "IdAutocomplete", new PackageAnalyzer());

            //TODO: we should be doing phrase queries to get the ordering right
            //const int MAX_NGRAM_LENGTH = 8;
            //q = (q.Length < MAX_NGRAM_LENGTH) ? q : q.Substring(0, MAX_NGRAM_LENGTH);
            //string phraseQuery = string.Format("IdAutocompletePhrase:\"/ {0}\"~20", q);
            //Query query = queryParser.Parse(phraseQuery);

            Query query = queryParser.Parse(q);

            Query boostedQuery = new RankingScoreQuery(query, rankings);
            return boostedQuery;
        }

        static Query AutoCompleteVersionMakeQuery(string id)
        {
            Query query = new TermQuery(new Term("Id", id.ToLowerInvariant()));
            return query;
        }

        public static string Find(IOwinContext context, NuGetSearcherManager searcherManager)
        {
            string id = context.Request.Query["id"];
            string scheme = context.Request.Uri.Scheme;
            return FindSearch(searcherManager, id, scheme);
        }

        static string FindSearch(NuGetSearcherManager searcherManager, string id, string scheme)
        {
            var searcher = searcherManager.Get();
            try
            {
                Query query = FindMakeQuery(id);
                TopDocs topDocs = searcher.Search(query, 1);
                return ResponseFormatter.FindMakeResult(searcher, scheme, topDocs);
            }
            finally
            {
                searcherManager.Release(searcher);
            }
        }

        static Query FindMakeQuery(string id)
        {
            string analyzedId = id.ToLowerInvariant();
            Query query = new TermQuery(new Term("Id", analyzedId));
            return query;
        }
    }
}