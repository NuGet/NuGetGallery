using Lucene.Net.Analysis;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers;
using Lucene.Net.Search;
using Lucene.Net.Search.Function;
using System;
using System.Collections.Generic;
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
            Func<string[], string[]> standardizeFields = (input) =>
            {
                string t = Fields
                    .Where((f) => f.Equals(input[0], StringComparison.InvariantCultureIgnoreCase))
                    .FirstOrDefault();

                return new[] { t, input[1] };
            };

            var clauses = queryParser.Parse(searchFilter.SearchTerm).Select(standardizeFields);

            var idSpecificTerms = clauses.Where((a) => string.Equals(a[0], "Id", StringComparison.InvariantCultureIgnoreCase));
            var fieldSpecificTerms = clauses.Where((a) => a[0] != null);
            var generalTerms = clauses.Where((a) => a[0] == null);
            
            var analyzer = new PerFieldAnalyzer();

            var idSpecificQueries = idSpecificTerms.Select(
                (c) => AnalysisHelper.GetFieldQuery(analyzer, c[0], c[1]));
            var fieldSpecificQueries = fieldSpecificTerms.Select(
                (c) => AnalysisHelper.GetFieldQuery(analyzer, c[0], c[1]));
            var generalQueries = generalTerms.Select(
                (c) => AnalysisHelper.GetMultiFieldQuery(analyzer, Fields, c[1]));

            // At this point we wonder...
            // What is the user intent?
            // General search? [foo bar]
            // Partially Id-targeted search? [id:Foo bar]
            // Other Field-targeted search? [author:Foo bar]
            // If field targeting is done, we should basically want to AND that with all other queries

            bool doExactId = !fieldSpecificQueries.Any();
            Query generalQuery = BuildGeneralQuery(doExactId, searchFilter.SearchTerm, analyzer, generalTerms, generalQueries);
            if (fieldSpecificQueries.Any())
            {
                BooleanQuery filteredQuery = new BooleanQuery();
                if (generalQueries.Any())
                {
                    filteredQuery.Add(generalQuery, BooleanClause.Occur.MUST);
                }

                foreach (var fieldQuery in fieldSpecificQueries)
                {
                    filteredQuery.Add(fieldQuery, BooleanClause.Occur.MUST);
                }

                generalQuery = filteredQuery;
            }

            if (searchFilter.SortProperty == SortProperty.Relevance)
            {
                // If searching by relevance, boost scores by download count.
                var downloadCountBooster = new FieldScoreQuery("DownloadCount", FieldScoreQuery.Type.INT);
                generalQuery = new CustomScoreQuery(generalQuery, downloadCountBooster);
            }

            return generalQuery;
        }

        private static Query BuildGeneralQuery(
            bool doExactId,
            string originalSearchText,
            Analyzer analyzer,
            IEnumerable<string[]> generalTerms, 
            IEnumerable<Query> generalQueries)
        {
            // All terms in the multi-term query appear in at least one of the target fields.
            var conjuctionQuery = new BooleanQuery();
            conjuctionQuery.SetBoost(2.0f);

            // Some terms in the multi-term query appear in at least one of the target fields.
            var disjunctionQuery = new BooleanQuery();
            disjunctionQuery.SetBoost(0.1f);

            // Suffix wildcard search e.g. jquer*
            var wildCardQuery = new BooleanQuery();
            wildCardQuery.SetBoost(0.5f);

            //var sanitizedTerms = GetSearchTerms(originalSearchText);

            //// Escape the final term for exact ID search
            //string exactId = string.Join(" ", sanitizedTerms);
            string escapedExactId = Escape(originalSearchText);

            var exactIdQuery = new TermQuery(new Term("Id-Exact", escapedExactId));
            exactIdQuery.SetBoost(2.5f);

            bool doNearlyExactId = generalTerms.Any();
            Query nearlyExactIdQuery = null;
            if (doNearlyExactId)
            {
                string escapedApproximateId = string.Join(" ", generalTerms.Select((c) => c[1]));
                nearlyExactIdQuery = AnalysisHelper.GetFieldQuery(analyzer, "Id", escapedApproximateId);
                nearlyExactIdQuery.SetBoost(2.0f);
            }

            var wildCardIdQuery = new WildcardQuery(new Term("Id-Exact", "*" + escapedExactId + "*"));

            foreach (var termQuery in generalQueries)
            {
                conjuctionQuery.Add(termQuery, BooleanClause.Occur.MUST);
                disjunctionQuery.Add(termQuery, BooleanClause.Occur.SHOULD);
            }

            var sanitizedTerms = generalTerms.Select((c) => c[1].ToLowerInvariant());
            foreach (var sanitizedTerm in sanitizedTerms)
            {
                foreach (var field in Fields)
                {
                    var wildCardTermQuery = new WildcardQuery(new Term(field, sanitizedTerm + "*"));
                    wildCardTermQuery.SetBoost(0.7f);
                    wildCardQuery.Add(wildCardTermQuery, BooleanClause.Occur.SHOULD);
                }
            }

            // OR of all the applicable queries
            List<Query> queriesToCombine = new List<Query>();
            if (doExactId)
            {
                queriesToCombine.AddRange(new Query[] { exactIdQuery, wildCardIdQuery });
            }

            if (doNearlyExactId)
            {
                queriesToCombine.Add(nearlyExactIdQuery);
            }

            queriesToCombine.AddRange(new Query[] { conjuctionQuery, disjunctionQuery, wildCardQuery });
            var query = conjuctionQuery.Combine(queriesToCombine.ToArray());
            return query;
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