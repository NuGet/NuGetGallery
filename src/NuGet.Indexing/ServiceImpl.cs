using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers;
using Lucene.Net.Search;
using Microsoft.Owin;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.Indexing
{
    public static class ServiceImpl
    {
        public static JToken Query(IOwinContext context, NuGetSearcherManager searcherManager)
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

            bool includeExplanation = false;
            if (!bool.TryParse(context.Request.Query["explanation"], out includeExplanation))
            {
                includeExplanation = false;
            }

            string projectType = context.Request.Query["projectType"] ?? string.Empty;

            string supportedFramework = context.Request.Query["supportedFramework"];

            string q = context.Request.Query["q"] ?? string.Empty;

            string scheme = context.Request.Uri.Scheme;

            return QuerySearch(searcherManager, scheme, q, countOnly, projectType, supportedFramework, includePrerelease, skip, take, includeExplanation);
        }

        public static JToken QuerySearch(NuGetSearcherManager searcherManager, string scheme, string q, bool countOnly, string projectType, string supportedFramework, bool includePrerelease, int skip, int take, bool includeExplanation)
        {
            IndexSearcher searcher = searcherManager.Get();
            try
            {
                Filter filter = searcherManager.GetFilter(includePrerelease, supportedFramework);

                //TODO: uncomment these lines when we have an index that contains the appropriate @type field in every document
                //Filter typeFilter = new CachingWrapperFilter(new TypeFilter("http://schema.nuget.org/schema#NuGetClassicPackage"));
                //filter = new ChainedFilter(new Filter[] { filter, typeFilter }, ChainedFilter.Logic.AND);

                Query query = MakeQuery(q, searcherManager);

                TopDocs topDocs = searcher.Search(query, filter, skip + take);

                return MakeResult(searcher, scheme, topDocs, skip, take, searcherManager, includeExplanation, query);
            }
            finally
            {
                searcherManager.Release(searcher);
            }
        }

        public static Query MakeQuery(string q, NuGetSearcherManager searcherManager)
        {
            Query query = LuceneQueryCreator.Parse(q, false);
            Query boostedQuery = new RankingScoreQuery(query, searcherManager.GetRankings());
            return boostedQuery;
        }

        public static JToken MakeResultData(IndexSearcher searcher, string scheme, TopDocs topDocs, int skip, int take, NuGetSearcherManager searcherManager, bool includeExplanation, Query query)
        {
            Uri registrationBaseAddress = searcherManager.RegistrationBaseAddress[scheme];

            JArray array = new JArray();

            for (int i = skip; i < Math.Min(skip + take, topDocs.ScoreDocs.Length); i++)
            {
                ScoreDoc scoreDoc = topDocs.ScoreDocs[i];

                Document document = searcher.Doc(scoreDoc.Doc);

                string url = document.Get("Url");
                string id = document.Get("Id");
                string version = document.Get("Version");

                JObject obj = new JObject();
                obj["@id"] = new Uri(registrationBaseAddress, url).AbsoluteUri;
                obj["@type"] = "Package";
                obj["registration"] = new Uri(registrationBaseAddress, string.Format("{0}/index.json", id.ToLowerInvariant())).AbsoluteUri;
                obj["id"] = id;

                AddField(obj, document, "domain", "Domain");
                AddField(obj, document, "description", "Description");
                AddField(obj, document, "summary", "Summary");
                AddField(obj, document, "title", "Title");
                AddField(obj, document, "iconUrl", "IconUrl");
                AddFieldAsArray(obj, document, "tags", "Tags");
                AddFieldAsArray(obj, document, "authors", "Authors");

                obj["version"] = version;
                obj["versions"] = searcherManager.GetVersions(scheme, scoreDoc.Doc);

                if (includeExplanation)
                {
                    Explanation explanation = searcher.Explain(query, scoreDoc.Doc);
                    obj["explanation"] = explanation.ToString();
                }

                array.Add(obj);
            }

            return array;
        }

        static void AddField(JObject obj, Document document, string to, string from)
        {
            string value = document.Get(from);
            if (value != null)
            {
                obj[to] = value;
            }
        }

        static void AddFieldAsArray(JObject obj, Document document, string to, string from)
        {
            string value = document.Get(from);
            if (value != null)
            {
                obj[to] = new JArray(value.Split(' '));
            }
        }

        static JToken MakeResult(IndexSearcher searcher, string scheme, TopDocs topDocs, int skip, int take, NuGetSearcherManager searcherManager, bool includeExplanation, Query query)
        {
            JToken data = MakeResultData(searcher, scheme, topDocs, skip, take, searcherManager, includeExplanation, query);

            JObject result = new JObject();

            result.Add("@context", new JObject { { "@vocab", "http://schema.nuget.org/schema#" } });
            result.Add("totalHits", topDocs.TotalHits);
            result.Add("lastReopen", searcherManager.LastReopen.ToString("o"));
            result.Add("index", searcherManager.IndexName);
            result.Add("data", data);

            return result;
        }

        public static JToken AutoComplete(IOwinContext context, NuGetSearcherManager searcherManager)
        {
            IndexSearcher searcher = searcherManager.Get();
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

                bool includeExplanation = false;
                if (!bool.TryParse(context.Request.Query["explanation"], out includeExplanation))
                {
                    includeExplanation = false;
                }

                string supportedFramework = context.Request.Query["supportedFramework"];

                string q = context.Request.Query["q"]; 
                string id = context.Request.Query["id"];

                if (q == null && id == null)
                {
                    q = string.Empty;
                }

                return AutoCompleteSearch(searcherManager, q, id, supportedFramework, includePrerelease, skip, take, includeExplanation);
            }
            finally
            {
                searcherManager.Release(searcher);
            }
        }

        static JToken AutoCompleteSearch(NuGetSearcherManager searcherManager, string q, string id, string supportedFramework, bool includePrerelease, int skip, int take, bool includeExplanation)
        {
            IndexSearcher searcher = searcherManager.Get();
            try
            {
                Filter filter = searcherManager.GetFilter(includePrerelease, supportedFramework);

                if (q != null)
                {
                    Query query = AutoCompleteMakeQuery(q, searcherManager);
                    TopDocs topDocs = searcher.Search(query, filter, skip + take);
                    return AutoCompleteMakeResult(searcher, topDocs, skip, take, searcherManager, includeExplanation, query);
                }
                else
                {
                    Query query = new TermQuery(new Term("Id", id.ToLowerInvariant()));
                    TopDocs topDocs = searcher.Search(query, filter, 1);
                    return AutoCompleteMakeVersionResult(searcherManager, includePrerelease, topDocs);
                }
            }
            finally
            {
                searcherManager.Release(searcher);
            }
        }

        static JObject AutoCompleteMakeVersionResult(NuGetSearcherManager searcherManager, bool includePrerelease, TopDocs topDocs)
        {
            JObject result = new JObject();

            result.Add("@context", new JObject { { "@vocab", "http://schema.nuget.org/schema#" } });
            result.Add("indexName", searcherManager.IndexName);

            if (topDocs.TotalHits > 0)
            {
                ScoreDoc scoreDoc = topDocs.ScoreDocs[0];
                JArray versions = searcherManager.GetVersionLists(scoreDoc.Doc);
                result.Add("totalHits", versions.Count());
                result["data"] = versions;
            }
            else
            {
                result.Add("totalHits", 0);
                result["data"] = new JArray();
            }
            return result;
        }

        static Query AutoCompleteMakeQuery(string q, NuGetSearcherManager searcherManager)
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

            Query boostedQuery = new RankingScoreQuery(query, searcherManager.GetRankings());
            return boostedQuery;
        }

        public static JToken AutoCompleteMakeResult(IndexSearcher searcher, TopDocs topDocs, int skip, int take, NuGetSearcherManager searcherManager, bool includeExplanation, Query query)
        {
            JArray array = new JArray();

            for (int i = skip; i < Math.Min(skip + take, topDocs.ScoreDocs.Length); i++)
            {
                ScoreDoc scoreDoc = topDocs.ScoreDocs[i];
                Document document = searcher.Doc(scoreDoc.Doc);
                string id = document.Get("Id");

                array.Add(id);
            }

            JObject result = new JObject();

            result.Add("@context", new JObject { { "@vocab", "http://schema.nuget.org/schema#" } });
            result.Add("totalHits", topDocs.TotalHits);
            result.Add("indexName", searcherManager.IndexName);
            result.Add("data", array);

            if (includeExplanation)
            {
                JArray explanations = new JArray();
                for (int i = skip; i < Math.Min(skip + take, topDocs.ScoreDocs.Length); i++)
                {
                    ScoreDoc scoreDoc = topDocs.ScoreDocs[i];
                    Explanation explanation = searcher.Explain(query, scoreDoc.Doc);
                    explanations.Add(explanation.ToString());
                }
                result.Add("explanations", explanations);
            }

            return result;
        }

        public static JToken Find(IOwinContext context, NuGetSearcherManager searcherManager)
        {
            string id = context.Request.Query["id"];

            string scheme = context.Request.Uri.Scheme;

            return FindSearch(searcherManager, id, scheme);
        }

        static JToken FindSearch(NuGetSearcherManager searcherManager, string id, string scheme)
        {
            if (id == null)
            {
                return null;
            }

            IndexSearcher searcher = searcherManager.Get();
            try
            {
                string analyzedId = id.ToLowerInvariant();
                Query query = new TermQuery(new Term("Id", analyzedId));
                TopDocs topDocs = searcher.Search(query, 1);

                if (topDocs.TotalHits > 0)
                {
                    Uri registrationBaseAddress = searcherManager.RegistrationBaseAddress[scheme];
                    JObject obj = new JObject();
                    obj["registration"] = new Uri(registrationBaseAddress, string.Format("{0}/index.json", id.ToLowerInvariant())).AbsoluteUri;
                    return obj;
                }
                else
                {
                    return null;
                }
            }
            finally
            {
                searcherManager.Release(searcher);
            }
        }
    }
}