using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Microsoft.Owin;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuGet.Indexing
{
    public static class ServiceInfoImpl
    {
        public static async Task TargetFrameworks(IOwinContext context, NuGetSearcherManager searcherManager)
        {
            IndexSearcher searcher = searcherManager.Get();

            try
            {
                HashSet<string> targetFrameworks = new HashSet<string>();

                IndexReader reader = searcher.IndexReader;

                for (int i = 0; i < reader.MaxDoc; i++)
                {
                    Document document = reader[i];

                    Field[] frameworks = document.GetFields("TargetFramework");

                    foreach (Field framework in frameworks)
                    {
                        targetFrameworks.Add(framework.StringValue);
                    }
                }

                JArray result = new JArray();
                foreach (string targetFramework in targetFrameworks)
                {
                    result.Add(targetFramework);
                }

                await ServiceHelpers.WriteResponse(context, System.Net.HttpStatusCode.OK, result);
            }
            finally
            {
                searcherManager.Release(searcher);
            }
        }

        public static async Task Segments(IOwinContext context, SearcherManager searcherManager)
        {
            searcherManager.MaybeReopen();

            IndexSearcher searcher = searcherManager.Get();

            try
            {
                IndexReader indexReader = searcher.IndexReader;

                JArray segments = new JArray();
                foreach (ReadOnlySegmentReader segmentReader in indexReader.GetSequentialSubReaders())
                {
                    JObject segmentInfo = new JObject();
                    segmentInfo.Add("segment", segmentReader.SegmentName);
                    segmentInfo.Add("documents", segmentReader.NumDocs());
                    segments.Add(segmentInfo);
                }

                await ServiceHelpers.WriteResponse(context, System.Net.HttpStatusCode.OK, segments);
            }
            finally
            {
                searcherManager.Release(searcher);
            }
        }

        //TODO: combine the following functions

        public static async Task Stats(IOwinContext context, NuGetSearcherManager searcherManager)
        {
            searcherManager.MaybeReopen();

            IndexSearcher searcher = searcherManager.Get();

            try
            {
                IndexReader indexReader = searcher.IndexReader;

                JObject result = new JObject();
                result.Add("numDocs", indexReader.NumDocs());
                result.Add("indexName", searcherManager.IndexName);
                result.Add("lastReopen", searcherManager.LastReopen.ToString("o"));

                await ServiceHelpers.WriteResponse(context, System.Net.HttpStatusCode.OK, result);
            }
            finally
            {
                searcherManager.Release(searcher);
            }
        }

        public static async Task Stats(IOwinContext context, SecureSearcherManager searcherManager)
        {
            searcherManager.MaybeReopen();

            IndexSearcher searcher = searcherManager.Get();

            try
            {
                IndexReader indexReader = searcher.IndexReader;

                JObject result = new JObject();
                result.Add("numDocs", indexReader.NumDocs());
                result.Add("indexName", searcherManager.IndexName);
                result.Add("lastReopen", searcherManager.LastReopen.ToString("o"));

                await ServiceHelpers.WriteResponse(context, System.Net.HttpStatusCode.OK, result);
            }
            finally
            {
                searcherManager.Release(searcher);
            }
        }
    }
}