using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Microsoft.Owin;
using Newtonsoft.Json.Linq;
using System;
using System.Net;
using System.Threading.Tasks;

namespace NuGet.Indexing
{
    public static class SecureQueryImpl
    {
        public static async Task Query(IOwinContext context, SecureSearcherManager searcherManager, string tenantId, string currentOwner)
        {
            int skip;
            if (!int.TryParse(context.Request.Query["skip"], out skip))
            {
                skip = 0;
            }

            int take;
            if (!int.TryParse(context.Request.Query["take"], out take))
            {
                take = 50;
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

            string q = context.Request.Query["q"] ?? string.Empty;

            string scheme = context.Request.Uri.Scheme;

            JToken result = Search(searcherManager, tenantId, currentOwner, scheme, q, countOnly, includePrerelease, skip, take, includeExplanation);

            await ServiceHelpers.WriteResponse(context, HttpStatusCode.OK, result);
        }

        public static JToken Search(SecureSearcherManager searcherManager, string tenantId, string currentOwner, string scheme, string q, bool countOnly, bool includePrerelease, int skip, int take, bool includeExplanation)
        {
            IndexSearcher searcher = searcherManager.Get();
            try
            {
                Filter filter = searcherManager.GetFilter(tenantId, new string [] { "http://schema.nuget.org/schema#ApiAppPackage" }, includePrerelease);

                Query query = MakeQuery(q);

                TopDocs topDocs = searcher.Search(query, filter, skip + take);

                return MakeResult(searcher, currentOwner, scheme, topDocs, skip, take, searcherManager, includeExplanation, query);
            }
            finally
            {
                searcherManager.Release(searcher);
            }
        }

        public static Query MakeQuery(string q)
        {
            Query query = LuceneQueryCreator.Parse(q, false);
            return query;
        }

        public static JToken MakeResultData(IndexSearcher searcher, string currentOwner, string scheme, TopDocs topDocs, int skip, int take, SecureSearcherManager searcherManager, bool includeExplanation, Query query)
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
                string owner = document.Get("Owner");

                JObject obj = new JObject();
                obj["@id"] = new Uri(registrationBaseAddress, url).AbsoluteUri;
                obj["@type"] = document.Get("@type"); ;
                obj["registration"] = new Uri(registrationBaseAddress, string.Format("{0}/index.json", id.ToLowerInvariant())).AbsoluteUri;
                obj["id"] = id;

                obj["isOwner"] = (owner == currentOwner);

                ServiceHelpers.AddField(obj, document, "packageContent", "PackageContent");
                ServiceHelpers.AddField(obj, document, "catalogEntry", "CatalogEntry");

                ServiceHelpers.AddField(obj, document, "tenantId", "TenantId");
                ServiceHelpers.AddField(obj, document, "namespace", "Namespace");
                ServiceHelpers.AddField(obj, document, "visibility", "Visibility");
                ServiceHelpers.AddField(obj, document, "description", "Description");
                ServiceHelpers.AddField(obj, document, "summary", "Summary");
                ServiceHelpers.AddField(obj, document, "title", "Title");
                ServiceHelpers.AddField(obj, document, "iconUrl", "IconUrl");
                ServiceHelpers.AddFieldAsObject(obj, document, "owner", "OwnerDetails");
                ServiceHelpers.AddFieldAsArray(obj, document, "tags", "Tags");
                ServiceHelpers.AddFieldAsArray(obj, document, "authors", "Authors");

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

        static JToken MakeResult(IndexSearcher searcher, string currentOwner, string scheme, TopDocs topDocs, int skip, int take, SecureSearcherManager searcherManager, bool includeExplanation, Query query)
        {
            JToken data = MakeResultData(searcher, currentOwner, scheme, topDocs, skip, take, searcherManager, includeExplanation, query);

            JObject result = new JObject();

            result.Add("@context", new JObject { { "@vocab", "http://schema.nuget.org/schema#" } });
            result.Add("totalHits", topDocs.TotalHits);
            result.Add("lastReopen", searcherManager.LastReopen.ToString("o"));
            result.Add("index", searcherManager.IndexName);
            result.Add("data", data);
            result.Add("currentOwner", currentOwner);

            return result;
        }

        public static async Task QueryByOwner(IOwinContext context, SecureSearcherManager searcherManager, string tenantId, string currentOwner)
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

            string scheme = context.Request.Uri.Scheme;

            JToken result = SearchByOwner(searcherManager, tenantId, scheme, currentOwner, countOnly, includePrerelease, skip, take, includeExplanation);

            await ServiceHelpers.WriteResponse(context, HttpStatusCode.OK, result);
        }

        public static JToken SearchByOwner(SecureSearcherManager searcherManager, string tenantId, string scheme, string currentOwner, bool countOnly, bool includePrerelease, int skip, int take, bool includeExplanation)
        {
            IndexSearcher searcher = searcherManager.Get();
            try
            {
                Filter filter = searcherManager.GetFilter(tenantId, new string[] { "http://schema.nuget.org/schema#ApiAppPackage" }, includePrerelease);

                Query query = new TermQuery(new Term("Owner", currentOwner));

                TopDocs topDocs = searcher.Search(query, filter, skip + take);

                return MakeResult(searcher, currentOwner, scheme, topDocs, skip, take, searcherManager, includeExplanation, query);
            }
            finally
            {
                searcherManager.Release(searcher);
            }
        }
    }
}