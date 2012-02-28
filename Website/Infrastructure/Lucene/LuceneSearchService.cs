using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.QueryParsers;
using Lucene.Net.Search;
using Lucene.Net.Index;

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
            var fields = new Dictionary<string, float> { { "Id", 1.2f }, { "Title", 1.0f }, { "Tags", 1.0f}, { "Description", 0.8f }, { "Author", 0.6f } };
            var analyzer = new StandardAnalyzer(LuceneCommon.LuceneVersion);
            searchTerm = QueryParser.Escape(searchTerm).ToLowerInvariant();

            var queryParser = new MultiFieldQueryParser(LuceneCommon.LuceneVersion, fields.Keys.ToArray(), analyzer, fields);

            var conjuctionQuery = new BooleanQuery();
            conjuctionQuery.SetBoost(1.5f);
            var disjunctionQuery = new BooleanQuery();
            var wildCardQuery = new BooleanQuery();
            wildCardQuery.SetBoost(0.7f);
            var exactIdQuery = new TermQuery(new Term("Id-Exact", searchTerm));
            exactIdQuery.SetBoost(2.5f);
            
            foreach(var term in searchTerm.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                conjuctionQuery.Add(queryParser.Parse(term), BooleanClause.Occur.MUST);
                disjunctionQuery.Add(queryParser.Parse(term), BooleanClause.Occur.SHOULD);
                
                foreach (var field in fields)
                {
                    var wildCardTermQuery = new WildcardQuery(new Term(field.Key, term + "*"));
                    wildCardTermQuery.SetBoost(0.7f * field.Value);
                    wildCardQuery.Add(wildCardTermQuery, BooleanClause.Occur.SHOULD);
                }
            }

            return conjuctionQuery.Combine(new Query[] { exactIdQuery, conjuctionQuery, disjunctionQuery, wildCardQuery });
        }
    }
}