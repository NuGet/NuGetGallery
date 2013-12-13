using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers;
using Lucene.Net.Search;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace NuGetGallery
{
    public static class Searcher
    {
        static IDictionary<string, Filter> _filters = new Dictionary<string, Filter>();

        public static string KeyRangeQuery(PackageSearcherManager searcherManager, int minKey, int maxKey)
        {
            //  for range queries we always want the IndexReader to be absolutely up to date

            searcherManager.MaybeReopen();

            IndexSearcher searcher = searcherManager.Get();

            try
            {
                NumericRangeQuery<int> numericRangeQuery = NumericRangeQuery.NewIntRange("Key", minKey, maxKey, true, true);

                JObject keys = new JObject();
                searcher.Search(numericRangeQuery, new KeyCollector(keys));

                return keys.ToString();
            }
            finally
            {
                searcherManager.Release(searcher);
            }
        }

        public static string Search(PackageSearcherManager searcherManager, string q, bool countOnly, string projectType, bool includePrerelease, string feed, string sortBy, int skip, int take, bool includeExplanation, bool ignoreFilter)
        {
            IndexSearcher searcher;

            try
            {
                if ((DateTime.UtcNow - searcherManager.WarmTimeStampUtc) > TimeSpan.FromMinutes(1))
                {
                    searcherManager.MaybeReopen();
                }

                searcher = searcherManager.Get();
            }
            catch (Exception e)
            {
                throw new CorruptIndexException("Exception on (re)opening", e); 
            }

            try
            {
                if (string.IsNullOrEmpty(q))
                {
                    //  these results are static and do not strictly require Lucene at all

                    if (countOnly)
                    {
                        return DocumentCount(searcher, includePrerelease, feed, ignoreFilter);
                    }
                    else
                    {
                        IDictionary<string, int> rankings = searcherManager.GetRankings(projectType);

                        return ListDocuments(searcher, rankings, includePrerelease, feed, sortBy, skip, take, includeExplanation, ignoreFilter);
                    }
                }
                else
                {
                    //  these are real query scenarios and absolutely require Lucene

                    if (countOnly)
                    {
                        return DocumentCountForQuery(searcher, q, includePrerelease, feed, ignoreFilter);
                    }
                    else
                    {
                        IDictionary<string, int> rankings = searcherManager.GetRankings(projectType);

                        return ListDocumentsForQuery(searcher, q, rankings, includePrerelease, feed, sortBy, skip, take, includeExplanation, ignoreFilter);
                    }
                }
            }
            finally
            {
                searcherManager.Release(searcher);
            }
        }

        private static string DocumentCount(IndexSearcher searcher, bool includePrerelease, string feed, bool ignoreFilter)
        {
            return DocumentCountImpl(searcher, new MatchAllDocsQuery(), includePrerelease, feed, ignoreFilter);
        }

        private static string ListDocuments(IndexSearcher searcher, IDictionary<string, int> rankings, bool includePrerelease, string feed, string sortBy, int skip, int take, bool includeExplanation, bool ignoreFilter)
        {
            return ListDocumentsImpl(searcher, new MatchAllDocsQuery(), rankings, includePrerelease, feed, sortBy, skip, take, includeExplanation, ignoreFilter);
        }

        private static string DocumentCountForQuery(IndexSearcher searcher, string q, bool includePrerelease, string feed, bool ignoreFilter)
        {
            return DocumentCountImpl(searcher, CreateBasicQuery(q), includePrerelease, feed, ignoreFilter);
        }

        private static string ListDocumentsForQuery(IndexSearcher searcher, string q, IDictionary<string, int> rankings, bool includePrerelease, string feed, string sortBy, int skip, int take, bool includeExplanation, bool ignoreFilter)
        {
            return ListDocumentsImpl(searcher, CreateBasicQuery(q), rankings, includePrerelease, feed, sortBy, skip, take, includeExplanation, ignoreFilter);
        }

        private static string DocumentCountImpl(IndexSearcher searcher, Query query, bool includePrerelease, string feed, bool ignoreFilter)
        {
            Filter filter = ignoreFilter ? null : GetFilter(includePrerelease, feed);

            TopDocs topDocs = searcher.Search(query, filter, 1);
            return MakeCountResult(topDocs.TotalHits);
        }

        private static string ListDocumentsImpl(IndexSearcher searcher, Query query, IDictionary<string, int> rankings, bool includePrerelease, string feed, string sortBy, int skip, int take, bool includeExplanation, bool ignoreFilter)
        {
            Filter filter = ignoreFilter ? null : GetFilter(includePrerelease, feed);

            Query boostedQuery = new RankingScoreQuery(query, rankings);
            
            int nDocs = GetDocsCount(skip, take);

            TopDocs topDocs = searcher.Search(boostedQuery, filter, nDocs);

            return MakeResults(searcher, topDocs, skip, take, includeExplanation, boostedQuery);
        }

        private static Filter GetFilter(bool includePrerelease, string feed)
        {
            string filterName = string.Format("{0}/{1}", includePrerelease ? "IsLatest" : "IsLatestStable", feed);

            Filter filter;
            if (!_filters.TryGetValue(filterName, out filter))
            {
                lock (_filters)
                {
                    Filter filter2;
                    if (!_filters.TryGetValue(filterName, out filter2))
                    {
                        filter2 = CreateFilter(includePrerelease, feed);
                        _filters.Add(filterName, filter2);
                    }

                    return filter2;
                }
            }

            return filter;
        }

        private static string GetFilterName(bool includePrerelease, string feed)
        {
            return string.Format("{0}/{1}", includePrerelease ? "IsLatest" : "IsLatestStable", feed);
        }

        private static Filter CreateFilter(bool includePrerelease, string feed)
        {
            if (feed == "none")
            {
                TermQuery filterQuery = new TermQuery(new Term(includePrerelease ? "IsLatest" : "IsLatestStable", "1"));
                return new CachingWrapperFilter(new QueryWrapperFilter(filterQuery));
            }
            else
            {
                BooleanQuery filterQuery = new BooleanQuery();
                filterQuery.Add(new TermQuery(new Term(includePrerelease ? "IsLatest" : "IsLatestStable", "1")), Occur.MUST);
                filterQuery.Add(new TermQuery(new Term("CuratedFeed", feed)), Occur.MUST);
                return new CachingWrapperFilter(new QueryWrapperFilter(filterQuery));
            }
        }

        private static string MakeCountResult(int totalHits)
        {
            return (new JObject { { "totalHits", totalHits } }).ToString();
        }

        private static string MakeResults(IndexSearcher searcher, TopDocs topDocs, int skip, int take, bool includeExplanation, Query query)
        {
            //  note the use of a StringBuilder because we have the response data already formatted as JSON in the fields in the index

            StringBuilder strBldr = new StringBuilder();

            strBldr.AppendFormat("{{\"totalHits\":{0},\"data\":[", topDocs.TotalHits);

            bool hasResult = false;

            for (int i = skip * take; i < topDocs.ScoreDocs.Length; i++)
            {
                ScoreDoc scoreDoc = topDocs.ScoreDocs[i];

                Document doc = searcher.Doc(scoreDoc.Doc);
                string data = doc.Get("Data");

                if (includeExplanation)
                {
                    data = AddExplanation(searcher, data, query, scoreDoc);
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

        private static string AddExplanation(IndexSearcher searcher, string data, Query query, ScoreDoc scoreDoc)
        {
            Explanation explanation = searcher.Explain(query, scoreDoc.Doc);

            JObject diagnostics = new JObject();

            diagnostics.Add("Rank", GetInt(searcher, scoreDoc.Doc, "Rank"));
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

        private static Query CreateBasicQuery(string q)
        {
            QueryParser parser = new PackageQueryParser(Lucene.Net.Util.Version.LUCENE_30, "Title", new PackageAnalyzer());
            Query query = parser.Parse(q);

            return query;
        }

        private static int GetDocsCount(int skip, int take)
        {
            return (skip + 1) * take;
        }
    }
}