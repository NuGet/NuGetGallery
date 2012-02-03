using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.QueryParsers;
using Lucene.Net.Search;
using System.Diagnostics.CodeAnalysis;

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
            if (!Directory.Exists(LuceneCommon.IndexPath))
            {
                return Enumerable.Empty<int>();
            }

            using (var directory = new LuceneFileSystem(LuceneCommon.IndexPath))
            {
                var searcher = new IndexSearcher(directory, readOnly: true);

                var boosts = new Dictionary<string, float> { { "Id-Exact", 5.0f }, { "Id", 2.0f }, { "Title", 1.5f }, { "Description", 0.8f } };
                var analyzer = new StandardAnalyzer(LuceneCommon.LuceneVersion);
                var queryParser = new MultiFieldQueryParser(LuceneCommon.LuceneVersion, new[] { "Id-Exact", "Id", "Title", "Author", "Description", "Tags" }, analyzer, boosts);

                var query = queryParser.Parse(searchTerm);
                var results = searcher.Search(query, filter: null, n: 1000, sort: Sort.RELEVANCE);
                var keys = results.scoreDocs.Select(c => Int32.Parse(searcher.Doc(c.doc).Get("Key"), CultureInfo.InvariantCulture))
                                            .ToList();
                searcher.Close();
                return keys;
            }
        }
    }
}