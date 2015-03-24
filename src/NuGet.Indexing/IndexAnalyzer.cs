using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Lucene.Net.Store.Azure;
using Lucene.Net.Store;

namespace NuGet.Indexing
{
    public static class IndexAnalyzer
    {
        public static string Analyze(PackageSearcherManager searcherManager)
        {
            searcherManager.MaybeReopen();
            
            IndexSearcher searcher = searcherManager.Get();

            try
            {
                IndexReader indexReader = searcher.IndexReader;

                JObject report = new JObject();

                report.Add("NumDocs", indexReader.NumDocs());
                report.Add("SearcherManagerIdentity", searcherManager.Id.ToString());

                AzureDirectory azDir = indexReader.Directory() as AzureDirectory;
                if (azDir != null)
                {
                    report.Add("Index", azDir.BlobContainer.Name);
                }
                else
                {
                    SimpleFSDirectory fsDir = indexReader.Directory() as SimpleFSDirectory;
                    if (fsDir != null)
                    {
                        report.Add("Index", fsDir.Directory.Name);
                    }
                }

                report.Add("RankingsUpdated", searcherManager.RankingsUpdatedUtc);
                report.Add("DownloadCountsUpdated", searcherManager.DownloadCountsUpdatedUtc);

                if (indexReader.CommitUserData != null)
                {
                    JObject commitUserdata = new JObject();
                    foreach (KeyValuePair<string, string> userData in indexReader.CommitUserData)
                    {
                        commitUserdata.Add(userData.Key, userData.Value);
                    }
                    report.Add("CommitUserData", commitUserdata);
                }

                // Moved segments to their own command since they can take a while to calculate in Azure

                return report.ToString();
            }
            finally
            {
                searcherManager.Release(searcher);
            }
        }

        public static string GetSegments(PackageSearcherManager searcherManager)
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
                return segments.ToString();
            }
            finally
            {
                searcherManager.Release(searcher);
            }
        }

        public static string GetDistinctStoredFieldNames(PackageSearcherManager searcherManager)
        {
            searcherManager.MaybeReopen();
            
            IndexSearcher searcher = searcherManager.Get();

            try
            {
                IndexReader indexReader = searcher.IndexReader;

                HashSet<string> distinctFieldNames = new HashSet<string>();

                for (int i = 0; i < indexReader.MaxDoc; i++)
                {
                    if (!indexReader.IsDeleted(i))
                    {
                        Document document = indexReader.Document(i);
                        IList<IFieldable> fields = document.GetFields();
                        foreach (IFieldable field in fields)
                        {
                            distinctFieldNames.Add(field.Name);
                        }
                    }
                }

                JArray array = new JArray();
                foreach (string fieldName in distinctFieldNames)
                {
                    array.Add(fieldName);
                }

                return array.ToString();
            }
            finally
            {
                searcherManager.Release(searcher);
            }
        }

        // Doesn't return JSON because consumers will want to make monitoring decisions based on this data as well as saving it/returning it from APIs
        public static IndexConsistencyReport GetIndexConsistency(PackageSearcherManager searcherManager, int databasePackageCount)
        {
            searcherManager.MaybeReopen();

            IndexSearcher searcher = searcherManager.Get();

            try
            {
                IndexReader indexReader = searcher.IndexReader;
                
                // Get the number of documents
                int numDocs = indexReader.NumDocs();

                // Build the report
                return new IndexConsistencyReport(numDocs, databasePackageCount);
            }
            finally
            {
                searcherManager.Release(searcher);
            }
        }
    }
}