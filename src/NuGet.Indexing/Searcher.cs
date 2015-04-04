using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers;
using Lucene.Net.Search;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Runtime.Versioning;

namespace NuGet.Indexing
{
    public static class Searcher
    {
        static DateTime WarmTimeStampUtc = DateTime.UtcNow;
        static IDictionary<string, Filter> _filters = new Dictionary<string, Filter>();

        public static string KeyRangeQuery(PackageSearcherManager searcherManager, int minKey, int maxKey)
        {
            //  for range queries we always want the IndexReader to be absolutely up to date

            searcherManager.MaybeReopen();

            IndexSearcher searcher = searcherManager.Get();

            try
            {
                NumericRangeQuery<int> numericRangeQuery = NumericRangeQuery.NewIntRange("Key", minKey, maxKey, true, true);

                List<DocumentKey> pairs = new List<DocumentKey>();
                searcher.Search(numericRangeQuery, new KeyCollector(pairs));

                // Group by key
                IEnumerable<IGrouping<int, DocumentKey>> groups = pairs.GroupBy(p => p.PackageKey);

                // De-duplicate
                IEnumerable<DocumentKey> deduped = groups.Select(g => g.First());
                
                JObject keys = new JObject();
                keys.Add(deduped.Select(p => new JProperty(p.PackageKey.ToString(), p.Checksum)));
                return keys.ToString();
            }
            finally
            {
                searcherManager.Release(searcher);
            }
        }

        public static string Search(PackageSearcherManager searcherManager, string q, bool countOnly, string projectType, bool includePrerelease, string feed, string sortBy, int skip, int take, bool includeExplanation, bool ignoreFilter)
        {
            return Search(
                searcherManager,
                LuceneQueryCreator.Parse(q, true),
                countOnly,
                projectType,
                includePrerelease,
                feed,
                sortBy,
                skip,
                take,
                includeExplanation,
                ignoreFilter);
        }

        public static string Search(PackageSearcherManager searcherManager, Query q, bool countOnly, string projectType, bool includePrerelease, string feed, string sortBy, int skip, int take, bool includeExplanation, bool ignoreFilter)
        {
            IndexSearcher searcher;

            try
            {
                if ((DateTime.UtcNow - WarmTimeStampUtc) > TimeSpan.FromMinutes(1))
                {
                    WarmTimeStampUtc = DateTime.UtcNow;

                    // Re-open on a background thread. We can safely continue to use the old reader while this happens.
                    Task.Factory
                        .StartNew(() => searcherManager.MaybeReopen())
                        .ContinueWith(t =>
                        {
                            // Log and suppress the exception to prevent taking down the whole process
                            if (t.IsFaulted)
                            {
                                Trace.WriteLine("Exception reopening searcher: {0}", t.Exception.ToString());

                                // Return a completed task indicating everything is A-OK :)
                                return Task.FromResult(0);
                            }
                            return t;
                        });
                }

                // Get the current reader. If a re-open is in progress but not yet complete, this will return the current reader.
                searcher = searcherManager.Get();
            }
            catch (Exception e)
            {
                throw new CorruptIndexException("Exception on (re)opening", e);
            }

            try
            {
                // ignoreFilter = true, don't filter by framework, feed or latest(stable)
                Filter filter = null;
                if (!ignoreFilter)
                {
                    // So if false, set up the filter and adjust the query for the framework if needed
                    filter = GetFilter(feed);
                }

                if (countOnly)
                {
                    return DocumentCountImpl(searcher, q, filter);
                }
                else
                {
                    IDictionary<string, int> rankings = searcherManager.GetRankings(projectType);

                    return ListDocumentsImpl(searcher, q, rankings, filter, sortBy, skip, take, includeExplanation, searcherManager);
                }
            }
            finally
            {
                searcherManager.Release(searcher);
            }
        }

        private static string DocumentCountImpl(IndexSearcher searcher, Query query, Filter filter)
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();
            TopDocs topDocs = searcher.Search(query, filter, 1);
            sw.Stop();
            return MakeCountResult(topDocs.TotalHits, sw.ElapsedMilliseconds);
        }

        private static string ListDocumentsImpl(IndexSearcher searcher, Query query, IDictionary<string, int> rankings, Filter filter, string sortBy, int skip, int take, bool includeExplanation, PackageSearcherManager manager)
        {
            Query boostedQuery = new RankingScoreQuery(query, rankings);
            
            int nDocs = GetDocsCount(skip, take);
            Sort sort = GetSort(sortBy);

            Stopwatch sw = new Stopwatch();
            sw.Start();
            TopDocs topDocs = (sort == null) ?
               searcher.Search(boostedQuery, filter, nDocs) :
               searcher.Search(boostedQuery, filter, nDocs, sort);
            sw.Stop();
            
            sw.Stop();
            return MakeResults(searcher, topDocs, skip, take, includeExplanation, boostedQuery, sw.ElapsedMilliseconds, rankings, manager);
        }

        private static readonly Dictionary<string, Func<Sort>> _sorts = new Dictionary<string, Func<Sort>>(StringComparer.OrdinalIgnoreCase) {
            {"lastEdited", () => new Sort(new SortField("EditedDate", SortField.INT, reverse: true))},
            {"published", () => new Sort(new SortField("PublishedDate", SortField.INT, reverse: true))},
            {"title-asc", () => new Sort(new SortField("DisplayName", SortField.STRING, reverse: false))},
            {"title-desc", () => new Sort(new SortField("DisplayName", SortField.STRING, reverse: true))},
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

        private static Filter GetFilter(string feed)
        {
            string filterName = feed;

            Filter filter;
            if (!_filters.TryGetValue(filterName, out filter))
            {
                lock (_filters)
                {
                    Filter filter2;
                    if (!_filters.TryGetValue(filterName, out filter2))
                    {
                        filter2 = CreateFilter(feed);
                        _filters.Add(filterName, filter2);
                    }

                    return filter2;
                }
            }

            return filter;
        }

        private static Filter CreateFilter(string feed)
        {
            if (feed == "none")
            {
                return null;
            }
            else
            {
                return new CachingWrapperFilter(new QueryWrapperFilter(new TermQuery(new Term("CuratedFeed", feed))));
            }
        }

        private static string MakeCountResult(int totalHits, long elapsed)
        {
            return (new JObject { { "totalHits", totalHits }, { "timeTakenInMs", elapsed } }).ToString();
        }

        private static string MakeResults(IndexSearcher searcher, TopDocs topDocs, int skip, int take, bool includeExplanation, Query query, long elapsed, IDictionary<string, int> rankings, PackageSearcherManager manager)
        {
            //  note the use of a StringBuilder because we have the response data already formatted as JSON in the fields in the index

            StringBuilder strBldr = new StringBuilder();

            string timestamp;
            if (!searcher.IndexReader.CommitUserData.TryGetValue("commit-time-stamp", out timestamp))
            {
                timestamp = null;
            }

            strBldr.AppendFormat("{{\"totalHits\":{0},\"timeTakenInMs\":{1},\"index\":\"{2}\"", topDocs.TotalHits, elapsed, manager.IndexName);
            if (!String.IsNullOrEmpty(timestamp))
            {
                strBldr.AppendFormat(",\"indexTimestamp\":\"{0}\"", timestamp);
            }
            if (includeExplanation)
            {
                // JsonConvert.Serialize does escaping and quoting.
                strBldr.AppendFormat(",\"executedQuery\":{0}", Newtonsoft.Json.JsonConvert.SerializeObject(query.ToString()));
            }
            strBldr.Append(",\"data\":[");

            bool hasResult = false;

            for (int i = skip; i < topDocs.ScoreDocs.Length; i++)
            {
                ScoreDoc scoreDoc = topDocs.ScoreDocs[i];

                Document doc = searcher.Doc(scoreDoc.Doc);
                string data = doc.Get("Data");

                string key = doc.Get("Key");
                int keyVal;
                if (!String.IsNullOrEmpty(key) && Int32.TryParse(key, out keyVal))
                {
                    DownloadCountRecord countRecord = manager.GetDownloadCounts(keyVal);
                    if (countRecord != null)
                    {
                        // Patch the data in to the JSON
                        JObject parsed = JObject.Parse(data);
                        parsed["DownloadCount"] = countRecord.Downloads;
                        parsed["PackageRegistration"]["DownloadCount"] = countRecord.RegistrationDownloads;
                        parsed.Add("Installs", countRecord.Installs);
                        parsed.Add("Updates", countRecord.Updates);
                        data = parsed.ToString(Formatting.None);
                    }
                }

                if (includeExplanation)
                {
                    data = AddExplanation(searcher, data, query, scoreDoc, rankings);
                }

                strBldr.Append(data);
                strBldr.Append(",");

                hasResult = true;
            }

            if (hasResult)
            {
                strBldr.Remove(strBldr.Length - 1, 1);
            }

            strBldr.Append("]}");

            string result = strBldr.ToString();

            return result;
        }

        private static JArray GetTerms(IndexSearcher searcher, int doc, string field)
        {
            TermPositionVector termPositionVector = (TermPositionVector)searcher.IndexReader.GetTermFreqVector(doc, field);

            if (termPositionVector == null)
            {
                return null;
            }

            JArray array = new JArray();

            for (int i = 0; i < termPositionVector.GetTerms().Length; i++)
            {
                string term = termPositionVector.GetTerms()[i];

                int[] positions = termPositionVector.GetTermPositions(i);

                string offset = "";
                foreach (TermVectorOffsetInfo offsetInfo in termPositionVector.GetOffsets(i))
                {
                    offset += string.Format("({0},{1})", offsetInfo.StartOffset, offsetInfo.EndOffset);
                }

                array.Add(term + " " + offset);
            }
            return array;
        }

        private static JToken GetInt(IndexSearcher searcher, int doc, string name)
        {
            IFieldable field = searcher.Doc(doc).GetFieldable(name);
            if (field != null)
            {
                string s = field.StringValue;
                int i = 0;
                if (int.TryParse(s, out i))
                {
                    return new JValue(i);
                }
            }
            return null;
        }

        private static JToken GetBool(IndexSearcher searcher, int doc, string name)
        {
            string s = searcher.Doc(doc).Get(name);
            if (s != null)
            {
                if (s == "0")
                {
                    return new JValue(false);
                }
                else if (s == "1")
                {
                    return new JValue(true);
                }
            }
            return null;
        }

        private static JArray GetMultiValue(IndexSearcher searcher, int doc, string name)
        {
            string[] values = searcher.Doc(doc).GetValues(name);
            JArray result = new JArray(values);
            return result;
        }

        private static HashSet<string> _fieldNames = new HashSet<string>(new string[] {
            "IdTerms",
            "TokenizedIdTerms",
            "ShingledIdTerms",
            "VersionTerms",
            "TitleTerms",
            "TagsTerms",
            "DescriptionTerms",
            "AuthorsTerms",
            "OwnersTerms",
            "PublishedDate",
            "EditedDate",
            "DisplayName",
            "IsLatest",
            "IsLatestStable",
            "CuratedFeed",
            "Key",
            "Rank"
        });

        private static bool IsProjectGuid(string name)
        {
            return !_fieldNames.Contains(name);
        }

        private static JArray GetProjectGuidRankings(IndexSearcher searcher, int doc)
        {
            JArray result = new JArray();
            Document document = searcher.Doc(doc);
            foreach (IFieldable fieldable in document.GetFields())
            {
                if (IsProjectGuid(fieldable.Name))
                {
                    int rank = 0;
                    int.TryParse(fieldable.StringValue, out rank);
                    result.Add(new JObject { { fieldable.Name, new JValue(rank) } });
                }
            }
            return result;
        }

        private static string AddExplanation(IndexSearcher searcher, string data, Query query, ScoreDoc scoreDoc, IDictionary<string, int> rankings)
        {
            Explanation explanation = searcher.Explain(query, scoreDoc.Doc);
            
            JObject diagnostics = new JObject();

            int rankVal;
            string id = searcher.Doc(scoreDoc.Doc).Get("Id");
            if (rankings.TryGetValue(id, out rankVal))
            {
                float rankingScore = RankingScoreQuery.GetRankingScore(rankings, id);
                diagnostics.Add("Rank", rankVal);
                diagnostics.Add("RankScore", rankingScore);
                diagnostics.Add("LuceneScore", scoreDoc.Score / rankingScore);
            }
            diagnostics.Add("Score", scoreDoc.Score.ToString());
            diagnostics.Add("Explanation", explanation.ToString());

            diagnostics.Add("IdTerms", GetTerms(searcher, scoreDoc.Doc, "Id"));
            diagnostics.Add("TokenizedIdTerms", GetTerms(searcher, scoreDoc.Doc, "TokenizedId"));
            diagnostics.Add("ShingledIdTerms", GetTerms(searcher, scoreDoc.Doc, "ShingledId"));
            diagnostics.Add("VersionTerms", GetTerms(searcher, scoreDoc.Doc, "Version"));
            diagnostics.Add("TitleTerms", GetTerms(searcher, scoreDoc.Doc, "Title"));
            diagnostics.Add("TagsTerms", GetTerms(searcher, scoreDoc.Doc, "Tags"));
            diagnostics.Add("DescriptionTerms", GetTerms(searcher, scoreDoc.Doc, "Description"));
            diagnostics.Add("AuthorsTerms", GetTerms(searcher, scoreDoc.Doc, "Authors"));
            diagnostics.Add("OwnersTerms", GetTerms(searcher, scoreDoc.Doc, "Owners"));

            diagnostics.Add("PublishedDate", GetInt(searcher, scoreDoc.Doc, "PublishedDate"));
            diagnostics.Add("EditedDate", GetInt(searcher, scoreDoc.Doc, "EditedDate"));

            diagnostics.Add("CuratedFeed", GetMultiValue(searcher, scoreDoc.Doc, "CuratedFeed"));
            diagnostics.Add("Key", GetInt(searcher, scoreDoc.Doc, "Key"));
            diagnostics.Add("Checksum", GetInt(searcher, scoreDoc.Doc, "Checksum"));
            diagnostics.Add("ProjectGuidRankings", GetProjectGuidRankings(searcher, scoreDoc.Doc));



            JObject obj = JObject.Parse(data);
            obj.Add("diagnostics", diagnostics);
            data = obj.ToString();

            return data;
        }

        private static int GetDocsCount(int skip, int take)
        {
            // Skip is the number of RECORDS to skip, Take is the number of RECORDS to take
            // Calculate the total to retrieve (because Lucene doesn't have a native concept of Skip)
            return skip + take;
        }
    }
}
