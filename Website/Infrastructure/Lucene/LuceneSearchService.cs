using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers;
using Lucene.Net.Search;
using Lucene.Net.Search.Function;

namespace NuGetGallery
{
    public class LuceneSearchService : ISearchService
    {
        public IQueryable<Package> Search(IQueryable<Package> packages, SearchFilter searchFilter, out int totalHits)
        {
            if (packages == null)
            {
                throw new ArgumentNullException("packages");
            }

            if (searchFilter == null)
            {
                throw new ArgumentNullException("searchFilter");
            }

            if (searchFilter.Skip < 0)
            {
                throw new ArgumentOutOfRangeException("skip");
            }

            if (searchFilter.Take < 0)
            {
                throw new ArgumentOutOfRangeException("take");
            }

            // For the given search term, find the keys that match.
            var keys = SearchCore(searchFilter, out totalHits);
            if (keys.Count == 0 || searchFilter.CountOnly)
            {
                return Enumerable.Empty<Package>().AsQueryable();
            }

            // Query the source for each of the keys that need to be taken.
            var results = packages.Where(p => keys.Contains(p.Key));

            // When querying the database, these keys are returned in no particular order. We use the original order of queries
            // and retrieve each of the packages from the result in the same order.
            var lookup = results.ToDictionary(p => p.Key, p => p);

            return keys.Select(key => LookupPackage(lookup, key))
                       .Where(p => p != null)
                       .AsQueryable();
        }

        private static Package LookupPackage(Dictionary<int, Package> dict, int key)
        {
            Package package;
            dict.TryGetValue(key, out package);
            return package;
        }

        private static IList<int> SearchCore(SearchFilter searchFilter, out int totalHits)
        {
            if (!Directory.Exists(LuceneCommon.IndexDirectory))
            {
                totalHits = 0;
                return new int[0];
            }

            SortField sortField = GetSortField(searchFilter);
            int numRecords = searchFilter.Skip + searchFilter.Take;

            using (var directory = new LuceneFileSystem(LuceneCommon.IndexDirectory))
            {
                var searcher = new IndexSearcher(directory, readOnly: true);
                var query = ParseQuery(searchFilter);

                var filterTerm = searchFilter.IncludePrerelease ? "IsLatest" : "IsLatestStable";
                var termQuery = new TermQuery(new Term(filterTerm, Boolean.TrueString));
                Filter filter = new QueryWrapperFilter(termQuery);
                

                var results = searcher.Search(query, filter: filter, n: numRecords, sort: new Sort(sortField));
                var keys = results.scoreDocs.Skip(searchFilter.Skip)
                                            .Select(c => ParseKey(searcher.Doc(c.doc).Get("Key")))
                                            .ToList();
                totalHits = results.totalHits;
                searcher.Close();
                return keys;
            }
        }

        private static Query ParseQuery(SearchFilter searchFilter)
        {
            if (String.IsNullOrWhiteSpace(searchFilter.SearchTerm))
            {
                return new MatchAllDocsQuery();
            }

            var fields = new[] { "Id", "Title", "Tags", "Description", "Author" };
            var analyzer = new StandardAnalyzer(LuceneCommon.LuceneVersion);
            var queryParser = new MultiFieldQueryParser(LuceneCommon.LuceneVersion, fields, analyzer);

            // All terms in the multi-term query appear in at least one of the fields.
            var conjuctionQuery = new BooleanQuery();
            conjuctionQuery.SetBoost(2.0f);

            // Some terms in the multi-term query appear in at least one of the fields.
            var disjunctionQuery = new BooleanQuery();
            disjunctionQuery.SetBoost(0.1f);

            // Suffix wildcard search e.g. jquer*
            var wildCardQuery = new BooleanQuery();
            wildCardQuery.SetBoost(0.5f);

            // Escape the entire term we use for exact searches.
            var escapedSearchTerm = Escape(searchFilter.SearchTerm);
            var exactIdQuery = new TermQuery(new Term("Id-Exact", escapedSearchTerm));
            exactIdQuery.SetBoost(2.5f);
            var wildCardIdQuery = new WildcardQuery(new Term("Id-Exact", "*" + escapedSearchTerm + "*"));

            foreach (var term in GetSearchTerms(searchFilter.SearchTerm))
            {
                var termQuery = queryParser.Parse(term);
                conjuctionQuery.Add(termQuery, BooleanClause.Occur.MUST);
                disjunctionQuery.Add(termQuery, BooleanClause.Occur.SHOULD);

                foreach (var field in fields)
                {
                    var wildCardTermQuery = new WildcardQuery(new Term(field, term + "*"));
                    wildCardTermQuery.SetBoost(0.7f);
                    wildCardQuery.Add(wildCardTermQuery, BooleanClause.Occur.SHOULD);
                }
            }
            
            // Create an OR of all the queries that we have
            var combinedQuery = conjuctionQuery.Combine(new Query[] { exactIdQuery, wildCardIdQuery, conjuctionQuery, disjunctionQuery, wildCardQuery });
            
            if (searchFilter.SortProperty == SortProperty.Relevance)
            {
                // If searching by relevance, boost scores by download count.
                var downloadCountBooster = new FieldScoreQuery("DownloadCount", FieldScoreQuery.Type.INT);
                return new CustomScoreQuery(combinedQuery, downloadCountBooster);
            }
            return combinedQuery;
        }

        private static IEnumerable<string> GetSearchTerms(string searchTerm)
        {
            return searchTerm.Split(new[] { ' ', '.', '-' }, StringSplitOptions.RemoveEmptyEntries)
                             .Concat(new[] { searchTerm })
                             .Distinct(StringComparer.OrdinalIgnoreCase)
                             .Select(Escape);
        }

        private static SortField GetSortField(SearchFilter searchFilter)
        {
            switch (searchFilter.SortProperty)
            {
                case SortProperty.DisplayName:
                    return new SortField("DisplayName", SortField.STRING, reverse: searchFilter.SortDirection == SortDirection.Descending);
                case SortProperty.DownloadCount:
                    return new SortField("DownloadCount", SortField.INT, reverse: true);
                case SortProperty.Recent:
                    return new SortField("PublishedDate", SortField.LONG, reverse: true);
            }
            return SortField.FIELD_SCORE;
        }

        private static string Escape(string term)
        {
            return QueryParser.Escape(term).ToLowerInvariant();
        }

        private static int ParseKey(string value)
        {
            int key;
            return Int32.TryParse(value, out key) ? key : 0;
        }
    }
}