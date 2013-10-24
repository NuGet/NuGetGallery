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
            if ((DateTime.UtcNow - searcherManager.WarmTimeStampUtc) > TimeSpan.FromMinutes(1))
            {
                searcherManager.MaybeReopen();
            }

            IndexSearcher searcher = searcherManager.Get();

            try
            {
                NumericRangeQuery<int> numericRangeQuery = NumericRangeQuery.NewIntRange("Key", minKey, maxKey, true, true);

                JArray keys = new JArray();
                searcher.Search(numericRangeQuery, new KeyCollector(keys));

                return keys.ToString();
            }
            finally
            {
                searcherManager.Release(searcher);
            }
        }

        public static string Search(PackageSearcherManager searcherManager, string q, bool countOnly, string projectType, bool includePrerelease, string feed, string sortBy, int page, bool includeExplanation, bool ignoreFilter)
        {
            if ((DateTime.UtcNow - searcherManager.WarmTimeStampUtc) > TimeSpan.FromMinutes(1))
            {
                searcherManager.MaybeReopen();
            }

            IndexSearcher searcher = searcherManager.Get();

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
                        return ListDocuments(searcher, projectType, includePrerelease, feed, sortBy, page, includeExplanation, ignoreFilter);
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
                        return ListDocumentsForQuery(searcher, q, projectType, includePrerelease, feed, sortBy, page, includeExplanation, ignoreFilter);
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

        private static string ListDocuments(IndexSearcher searcher, string projectType, bool includePrerelease, string feed, string sortBy, int page, bool includeExplanation, bool ignoreFilter)
        {
            return ListDocumentsImpl(searcher, new MatchAllDocsQuery(), projectType, includePrerelease, feed, sortBy, page, includeExplanation, ignoreFilter);
        }

        private static string DocumentCountForQuery(IndexSearcher searcher, string q, bool includePrerelease, string feed, bool ignoreFilter)
        {
            return DocumentCountImpl(searcher, CreateBasicQuery(q), includePrerelease, feed, ignoreFilter);
        }

        private static string ListDocumentsForQuery(IndexSearcher searcher, string q, string projectType, bool includePrerelease, string feed, string sortBy, int page, bool includeExplanation, bool ignoreFilter)
        {
            return ListDocumentsImpl(searcher, CreateBasicQuery(q), projectType, includePrerelease, feed, sortBy, page, includeExplanation, ignoreFilter);
        }

        private static string DocumentCountImpl(IndexSearcher searcher, Query query, bool includePrerelease, string feed, bool ignoreFilter)
        {
            Filter filter = ignoreFilter ? null : GetFilter(includePrerelease, feed);

            TopDocs topDocs = searcher.Search(query, filter, 1);
            return MakeCountResult(topDocs.TotalHits);
        }

        private static string ListDocumentsImpl(IndexSearcher searcher, Query query, string projectType, bool includePrerelease, string feed, string sortBy, int page, bool includeExplanation, bool ignoreFilter)
        {
            Filter filter = ignoreFilter ? null : GetFilter(includePrerelease, feed);

            string rank = (string.IsNullOrEmpty(projectType)) ? "Rank" : projectType;
            Query boostedQuery = new RankingBoostingQuery(query, rank);

            int nDocs = GetDocsCount(page);
            Sort sort = GetSort(sortBy, rank);

            TopDocs topDocs = (sort == null) ? searcher.Search(boostedQuery, filter, nDocs) : searcher.Search(boostedQuery, filter, nDocs, sort);

            return MakeResults(searcher, topDocs, page, includeExplanation, boostedQuery);
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

        private static string MakeCountResult(int count)
        {
            return (new JObject { { "count", count } }).ToString();
        }

        private static string MakeResults(IndexSearcher searcher, TopDocs topDocs, int page, bool includeExplanation, Query query)
        {
            StringBuilder strBldr = new StringBuilder();

            strBldr.Append("[");

            bool hasResult = false;

            for (int i = (page - 1) * 20; i < topDocs.ScoreDocs.Length; i++)
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

            strBldr.Append("]");

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

        private static string AddExplanation(IndexSearcher searcher, string data, Query query, ScoreDoc scoreDoc)
        {
            Explanation explanation = searcher.Explain(query, scoreDoc.Doc);

            JObject obj = JObject.Parse(data);
            obj.Add("Score", scoreDoc.Score.ToString());
            obj.Add("Explanation", explanation.ToString());

            obj.Add("IdTerms", GetTerms(searcher, scoreDoc.Doc, "Id"));
            obj.Add("TokenizedIdTerms", GetTerms(searcher, scoreDoc.Doc, "TokenizedId"));
            obj.Add("VersionTerms", GetTerms(searcher, scoreDoc.Doc, "Version"));
            obj.Add("TitleTerms", GetTerms(searcher, scoreDoc.Doc, "Title"));
            obj.Add("TagsTerms", GetTerms(searcher, scoreDoc.Doc, "Tags"));
            obj.Add("DescriptionTerms", GetTerms(searcher, scoreDoc.Doc, "Description"));
            obj.Add("AuthorsTerms", GetTerms(searcher, scoreDoc.Doc, "Authors"));
            obj.Add("OwnersTerms", GetTerms(searcher, scoreDoc.Doc, "Owners"));

            data = obj.ToString();

            return data;
        }

        private static Query CreateBasicQuery(string q)
        {
            QueryParser parser = new PackageQueryParser(Lucene.Net.Util.Version.LUCENE_30, "Title", new PackageAnalyzer());
            Query query = parser.Parse(q);

            return query;
        }

        private static int GetDocsCount(int page)
        {
            return page * 20;
        }

        private static Sort GetSort(string sortBy, string rank)
        {
            switch (sortBy)
            {
                case "relevance":
                    break;
                case "rank":
                    return new Sort(new SortField(rank, SortField.INT));
                case "title-asc":
                    return new Sort(new SortField("DisplayName", SortField.STRING));
                case "title-desc":
                    return new Sort(new SortField("DisplayName", SortField.STRING, true));
                case "published":
                    return new Sort(new SortField("PublishedDate", SortField.INT, true));
                case "last-edited":
                    return new Sort(new SortField("EditedDate", SortField.INT, true));
            }
            return null;
        }
    }
}