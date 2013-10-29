using Lucene.Net.Index;
using Lucene.Net.Search;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace NuGetGallery
{
    public static class IndexAnalyzer
    {
        public static string Analyze(PackageSearcherManager searcherManager)
        {
            if ((DateTime.UtcNow - searcherManager.WarmTimeStampUtc) > TimeSpan.FromMinutes(1))
            {
                searcherManager.MaybeReopen();
            }

            IndexSearcher searcher = searcherManager.Get();

            try
            {
                IndexReader indexReader = searcher.IndexReader;

                JObject report = new JObject();

                report.Add("TotalMemory", GC.GetTotalMemory(false));

                report.Add("NumDocs", indexReader.NumDocs());

                if (indexReader.CommitUserData != null)
                {
                    JObject commitUserdata = new JObject();
                    foreach (KeyValuePair<string, string> userData in indexReader.CommitUserData)
                    {
                        commitUserdata.Add(userData.Key, userData.Value);
                    }
                    report.Add("CommitUserData", commitUserdata);
                }

                JArray segments = new JArray();
                foreach (ReadOnlySegmentReader segmentReader in indexReader.GetSequentialSubReaders())
                {
                    JObject segmentInfo = new JObject();
                    segmentInfo.Add("segment", segmentReader.SegmentName);
                    segmentInfo.Add("documents", segmentReader.NumDocs());
                    segments.Add(segmentInfo);
                }
                report.Add("Segments", segments);

                return report.ToString();
            }
            finally
            {
                searcherManager.Release(searcher);
            }
        }
    }
}