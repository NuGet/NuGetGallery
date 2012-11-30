using Lucene.Net.Analysis.Standard;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers;
using Lucene.Net.Search;
using Lucene.Net.Search.Function;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NuGetGallery
{
    public class LuceneSearchService : ISearchService
    {
        private Lucene.Net.Store.Directory _directory;

        private static readonly string[] Fields = new[] { "Id", "Title", "Tags", "Description", "Author" };

        public LuceneSearchService(Lucene.Net.Store.Directory directory)
        {
            _directory = directory;
        }

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
                throw new ArgumentOutOfRangeException("searchFilter");
            }

            if (searchFilter.Take < 0)
            {
                throw new ArgumentOutOfRangeException("searchFilter");
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

        private IList<int> SearchCore(SearchFilter searchFilter, out int totalHits)
        {
            SortField sortField = GetSortField(searchFilter);
            int numRecords = searchFilter.Skip + searchFilter.Take;

            IndexSearcher searcher;
            searcher = new IndexSearcher(_directory, readOnly: true);

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

        private static Query ParseQuery(SearchFilter searchFilter)
        {
            if (String.IsNullOrWhiteSpace(searchFilter.SearchTerm))
            {
                return new MatchAllDocsQuery();
            }

            // 1. parse the query into field clauses and general terms
            // we imagine that mostly, field clauses are meant to 'filter' results found searching for general terms
            var queryParser = new NuGetQueryParser();
            
            // for field names that aren't actually searchable fields treat them as general terms
            Func<string[],string[]> standardizeFields = (string[] s) =>
            {
                string t = Fields.Where((f) => f.Equals(s[0], StringComparison.InvariantCultureIgnoreCase)).FirstOrDefault();
                return new[] { t, s[1], s[2] };
            };

            var clauses = queryParser.Parse(searchFilter.SearchTerm).list.Select(standardizeFields);
            var fieldSpecificClauses = clauses.Where((a) => a[0] != null);
            var generalTerms = clauses.Where((a) => a[0] == null);
            var analyzer = new PerFieldAnalyzer();

            var fieldSpecificQueries = fieldSpecificClauses.Select(
                (c) => AnalysisHelper.GetFieldQuery(analyzer, c[0], c[1] ?? c[2]));

            var generalQueries = generalTerms.Select(
                (c) => AnalysisHelper.GetMultiFieldQuery(analyzer, Fields, c[1] ?? c[2]));

            // All terms in the multi-term query appear in at least one of the target fields.
            var conjuctionQuery = new BooleanQuery();
            conjuctionQuery.SetBoost(2.0f);

            // Some terms in the multi-term query appear in at least one of the target fields.
            var disjunctionQuery = new BooleanQuery();
            disjunctionQuery.SetBoost(0.1f);

            // Suffix wildcard search e.g. jquer*
            var wildCardQuery = new BooleanQuery();
            wildCardQuery.SetBoost(0.5f);

            var sanitizedTerms = generalTerms.Select((c) => (c[1] ?? c[2]).ToLowerInvariant());

            Query executionQuery = null;

            // Escape the final term for exact ID search
            string exactId = string.Join(" ", sanitizedTerms);
            string escapedExactId = Escape(exactId);

            var exactIdQuery = new TermQuery(new Term("Id-Exact", escapedExactId));
            exactIdQuery.SetBoost(2.5f);

            var wildCardIdQuery = new WildcardQuery(new Term("Id-Exact", "*" + escapedExactId + "*"));

            foreach (var termQuery in generalQueries.Concat(fieldSpecificQueries))
            {
                conjuctionQuery.Add(termQuery, BooleanClause.Occur.MUST);
                disjunctionQuery.Add(termQuery, BooleanClause.Occur.SHOULD);
            }

            foreach (var sanitizedTerm in sanitizedTerms)
            {
                foreach (var field in Fields)
                {
                    var wildCardTermQuery = new WildcardQuery(new Term(field, sanitizedTerm + "*"));
                    wildCardTermQuery.SetBoost(0.7f);
                    wildCardQuery.Add(wildCardTermQuery, BooleanClause.Occur.SHOULD);
                }
            }

            // Create an OR of all the queries that we have
            executionQuery = 
                conjuctionQuery.Combine(new Query[] { exactIdQuery, wildCardIdQuery, conjuctionQuery, disjunctionQuery, wildCardQuery });

            if (searchFilter.SortProperty == SortProperty.Relevance)
            {
                // If searching by relevance, boost scores by download count.
                var downloadCountBooster = new FieldScoreQuery("DownloadCount", FieldScoreQuery.Type.INT);
                return new CustomScoreQuery(executionQuery, downloadCountBooster);
            }
            else
            {
                return executionQuery;
            }
        }

        private static IEnumerable<string> GetSearchTerms(string searchTerm)
        {
            return searchTerm.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
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