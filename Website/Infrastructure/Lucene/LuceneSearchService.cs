using Lucene.Net.Analysis.Standard;
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

            var analyzer = new PerFieldAnalyzer();
            var queryParser = new MultiFieldQueryParser(LuceneCommon.LuceneVersion, Fields, analyzer);

            // All terms in the multi-term query appear in at least one of the fields.
            var conjuctionQuery = new BooleanQuery();
            conjuctionQuery.SetBoost(2.0f);

            // Some terms in the multi-term query appear in at least one of the fields.
            var disjunctionQuery = new BooleanQuery();
            disjunctionQuery.SetBoost(0.1f);

            // Suffix wildcard search e.g. jquer*
            var wildCardQuery = new BooleanQuery();
            wildCardQuery.SetBoost(0.5f);

            // Cleanup the search terms, and analyze the user intent - is this an ID search?
            bool specificallySearchingNonIdFields = false;
            var sanitizedTerms = GetSanitizedTerms(searchFilter.SearchTerm, out specificallySearchingNonIdFields);

            Query executionQuery = null;
            if (specificallySearchingNonIdFields)
            {
                // Don't do exact ID search or wildcard ID search
                // Don't do our fancy optimizations
                // Just rely on Lucene Query parser to do the right thing
                executionQuery = queryParser.Parse(searchFilter.SearchTerm);
            }
            else 
            {
                // Escape the final term for exact ID search
                string exactId = string.Join(" ", sanitizedTerms);
                string escapedExactId = Escape(exactId);

                var exactIdQuery = new TermQuery(new Term("Id-Exact", escapedExactId));
                exactIdQuery.SetBoost(2.5f);

                var wildCardIdQuery = new WildcardQuery(new Term("Id-Exact", "*" + escapedExactId + "*"));

                foreach (var term in GetSearchTerms(searchFilter.SearchTerm))
                {
                    var termQuery = queryParser.Parse(term);
                    conjuctionQuery.Add(termQuery, BooleanClause.Occur.MUST);
                    disjunctionQuery.Add(termQuery, BooleanClause.Occur.SHOULD);

                    // It might have been a field specific query?
                    string justOneField = null;
                    if (termQuery is TermQuery)
                    {
                        justOneField = (termQuery as TermQuery).GetTerm().Field();
                    }

                    // Or it might not.
                    foreach (var field in (justOneField == null ? Fields : new[] { justOneField }))
                    {
                        var wildCardTermQuery = new WildcardQuery(new Term(field, term + "*"));
                        wildCardTermQuery.SetBoost(0.7f);
                        wildCardQuery.Add(wildCardTermQuery, BooleanClause.Occur.SHOULD);
                    }
                }

                // Create an OR of all the queries that we have
                executionQuery = 
                    conjuctionQuery.Combine(new Query[] { exactIdQuery, wildCardIdQuery, conjuctionQuery, disjunctionQuery, wildCardQuery });
            }

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

        // Strip out LUCENE search syntax-isms.
        // 'OR, 'AND', and '-[term]' get dropped
        // '+[term]' and '[field]:[term]' get returned as 'term'
        private static IEnumerable<string> GetSanitizedTerms(string searchTerm, out bool searchesNonIdFields)
        {
            List<String> ret = new List<string>();
            searchesNonIdFields = false;
            var parts = searchTerm.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                if (string.Equals(part, "OR", StringComparison.InvariantCultureIgnoreCase)
                    || string.Equals(part, "AND", StringComparison.InvariantCultureIgnoreCase)
                    || part.StartsWith("-", StringComparison.InvariantCultureIgnoreCase))
                {
                    continue;
                }

                string p = part;
                if (p.StartsWith("+"))
                {
                    p = p.Substring(1);
                }

                foreach (var field in Fields)
                {
                    if (p.StartsWith(field, StringComparison.InvariantCultureIgnoreCase) 
                        && p[field.Length] == ':')
                    {
                        if (field != "Id")
                        {
                            searchesNonIdFields = true;
                        }

                        p = p.Substring(field.Length + 1);
                        break;
                    }
                }

                ret.Add(p);
            }

            return ret;
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