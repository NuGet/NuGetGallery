using System;
using System.Collections.Generic;
using System.Globalization;
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
        private const int MaximumRecordsToReturn = 1000;

        public IQueryable<Package> Search(IQueryable<Package> packages, string searchTerm)
        {
            if (String.IsNullOrEmpty(searchTerm))
            {
                return packages;
            }
            var keys = SearchCore(searchTerm);
            return SearchByKeys(packages, keys);
        }

        public IQueryable<Package> SearchWithRelevance(IQueryable<Package> packages, string searchTerm)
        {
            int numberOfHits;
            return SearchWithRelevance(packages, searchTerm, MaximumRecordsToReturn, out numberOfHits);
        }

        public IQueryable<Package> SearchWithRelevance(IQueryable<Package> packages, string searchTerm, int take, out int numberOfHits)
        {
            numberOfHits = 0;
            if (String.IsNullOrEmpty(searchTerm))
            {
                return packages;
            }

            var keys = SearchCore(searchTerm);
            if (!keys.Any())
            {
                return Enumerable.Empty<Package>().AsQueryable();
            }

            numberOfHits = keys.Count();
            var results = SearchByKeys(packages, keys.Take(take));
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

        private static IQueryable<Package> SearchByKeys(IQueryable<Package> packages, IEnumerable<int> keys)
        {
            return packages.Where(p => keys.Contains(p.Key));
        }

        private static IEnumerable<int> SearchCore(string searchTerm)
        {
            if (!Directory.Exists(LuceneCommon.IndexDirectory))
            {
                return Enumerable.Empty<int>();
            }

            using (var directory = new LuceneFileSystem(LuceneCommon.IndexDirectory))
            {
                var searcher = new IndexSearcher(directory, readOnly: true);
                var query = ParseQuery(searchTerm);
                var results = searcher.Search(query, filter: null, n: 1000, sort: new Sort(new[] { SortField.FIELD_SCORE, new SortField("DownloadCount", SortField.INT, reverse: true) }));
                var keys = results.scoreDocs.Select(c => Int32.Parse(searcher.Doc(c.doc).Get("Key"), CultureInfo.InvariantCulture))
                                            .ToList();
                searcher.Close();
                return keys;
            }
        }

        private static Query ParseQuery(string searchTerm)
        {
            var fields = new Dictionary<string, float> { { "Id", 1.2f }, { "Title", 1.0f }, { "Tags", 0.8f }, { "Description", 0.1f }, 
                                                         { "Author", 1.0f } };
            var analyzer = new StandardAnalyzer(LuceneCommon.LuceneVersion);
            var queryParser = new MultiFieldQueryParser(LuceneCommon.LuceneVersion, fields.Keys.ToArray(), analyzer, fields);

            var conjuctionQuery = new BooleanQuery();
            conjuctionQuery.SetBoost(2.0f);
            var disjunctionQuery = new BooleanQuery();
            disjunctionQuery.SetBoost(0.1f);
            var wildCardQuery = new BooleanQuery();
            wildCardQuery.SetBoost(0.5f);

            // Escape the entire term we use for exact searches.
            var escapedSearchTerm = Escape(searchTerm);
            var exactIdQuery = new TermQuery(new Term("Id-Exact", escapedSearchTerm));
            exactIdQuery.SetBoost(2.5f);
            var wildCardIdQuery = new WildcardQuery(new Term("Id-Exact", "*" + escapedSearchTerm + "*"));
            
            foreach(var term in GetSearchTerms(searchTerm))
            {
                var termQuery = queryParser.Parse(term);
                conjuctionQuery.Add(termQuery, BooleanClause.Occur.MUST);
                disjunctionQuery.Add(termQuery, BooleanClause.Occur.SHOULD);

                foreach (var field in fields)
                {
                    var wildCardTermQuery = new WildcardQuery(new Term(field.Key, term + "*"));
                    wildCardTermQuery.SetBoost(0.7f * field.Value);
                    wildCardQuery.Add(wildCardTermQuery, BooleanClause.Occur.SHOULD);
                }
            }

            var downloadCountBooster = new FieldScoreQuery("DownloadCount", FieldScoreQuery.Type.INT);
            return new CustomScoreQuery(conjuctionQuery.Combine(new Query[] { exactIdQuery, wildCardIdQuery, conjuctionQuery, disjunctionQuery, wildCardQuery }),
                                       downloadCountBooster);
        }

        private static IEnumerable<string> GetSearchTerms(string searchTerm)
        {
            return searchTerm.Split(new[] { ' ', '.', '-' }, StringSplitOptions.RemoveEmptyEntries)
                             .Concat(new[] { searchTerm })
                             .Distinct(StringComparer.OrdinalIgnoreCase)
                             .Select(Escape);
        }

        private static string Escape(string term)
        {
            return QueryParser.Escape(term).ToLowerInvariant();
        }
    }
}