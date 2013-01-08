using System.Collections.ObjectModel;
using System.Diagnostics;
using Lucene.Net.Analysis;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Search.Function;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;

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

            return SearchCore(searchFilter, out totalHits);
        }

        private IQueryable<Package> SearchCore(SearchFilter searchFilter, out int totalHits)
        {
            int numRecords = searchFilter.Skip + searchFilter.Take;

            var searcher = new IndexSearcher(_directory, readOnly: true);
            var query = ParseQuery(searchFilter);

            // IF searching by relevance, boost scores by download count.
            if (searchFilter.SortProperty == SortProperty.Relevance)
            {
                var downloadCountBooster = new FieldScoreQuery("DownloadCount", FieldScoreQuery.Type.INT);
                query = new CustomScoreQuery(query, downloadCountBooster);
            }

            var filterTerm = searchFilter.IncludePrerelease ? "IsLatest" : "IsLatestStable";
            var termQuery = new TermQuery(new Term(filterTerm, Boolean.TrueString));
            var filter = new QueryWrapperFilter(termQuery);
            var results = searcher.Search(query, filter: filter, n: numRecords, sort: new Sort(GetSortField(searchFilter)));
            totalHits = results.totalHits;

            if (results.totalHits == 0 || searchFilter.CountOnly)
            {
                return Enumerable.Empty<Package>().AsQueryable();
            }

            var packages = results.scoreDocs
                                  .Skip(searchFilter.Skip)
                                  .Select(sd => PackageFromDoc(searcher.Doc(sd.doc)))
                                  .ToList();
            return packages.AsQueryable();
        }

        private static Package PackageFromDoc(Document doc)
        {
            int dlc = Int32.Parse(doc.Get("DownloadCount"), CultureInfo.InvariantCulture);
            int key = Int32.Parse(doc.Get("Key"), CultureInfo.InvariantCulture);
            int prk = Int32.Parse(doc.Get("PackageRegistrationKey"), CultureInfo.InvariantCulture);
            bool isLatest = Boolean.Parse(doc.Get("IsLatest"));
            bool isLatestStable = Boolean.Parse(doc.Get("IsLatestStable"));
            string owners = doc.Get("Owners");
            string[] ownersSplit;
            if (owners != null)
            {
                ownersSplit = owners.Split(';');
            }
            else
            {
                ownersSplit = new string[]{};
            }
            return new Package
            {
                Description = doc.Get("Description"),
                FlattenedAuthors = doc.Get("Authors"),
                IconUrl = doc.Get("IconUrl"),
                IsLatest = isLatest,
                IsLatestStable = isLatestStable,
                Key = key,
                Title = doc.Get("Title"),
                PackageRegistration = new PackageRegistration
                {
                    Id = doc.Get("Id-Original"),
                    DownloadCount = dlc,
                    Key = prk,
                    Owners = ownersSplit.Select(o => new User { Username = o }).ToList()
                },
                PackageRegistrationKey = prk,
                Tags = doc.Get("Tags"),
            };
        }

        private static Query ParseQuery(SearchFilter searchFilter)
        {
            // 1. parse the query into field clauses and general terms
            // We imagine that mostly, field clauses are meant to 'filter' results found searching for general terms.
            // The resulting clause collections may be empty.
            var queryParser = new NuGetQueryParser();
            var clauses = queryParser.Parse(searchFilter.SearchTerm).Select(StandardizeSearchTerms).ToList();
            var fieldSpecificTerms = clauses.Where(a => a.Field != null);
            var generalTerms = clauses.Where(a => a.Field == null);

            // Convert terms to appropriate Lucene Query objects
            var analyzer = new PerFieldAnalyzer();
            var fieldSpecificQueries = fieldSpecificTerms
                .Select(c => AnalysisHelper.GetFieldQuery(analyzer, c.Field, c.TermOrPhrase))
                .Where(q => !IsDegenerateQuery(q))
                .ToList();

            var generalQueries = generalTerms
                .Select(c => AnalysisHelper.GetMultiFieldQuery(analyzer, Fields, c.TermOrPhrase))
                .Where(q => !IsDegenerateQuery(q))
                .ToList();

            if (fieldSpecificQueries.Count == 0 &&
                generalQueries.Count == 0)
            {
                return new MatchAllDocsQuery();
            }

            // At this point we try to detect user intent...
            // a) General search? [foo bar]
            // b) Id-targeted search? [id:Foo bar]
            // c)  Other Field-targeted search? [author:Foo bar]
            bool doExactId = !fieldSpecificQueries.Any();
            Query generalQuery = BuildGeneralQuery(doExactId, searchFilter.SearchTerm, analyzer, generalTerms, generalQueries);

            // IF  field targeting is done, we should basically want to AND their field specific queries with all other query terms
            if (fieldSpecificQueries.Any())
            {
                var combinedQuery = new BooleanQuery();

                if (!IsDegenerateQuery(combinedQuery))
                {
                    combinedQuery.Add(generalQuery, BooleanClause.Occur.MUST);
                }

                foreach (var fieldQuery in fieldSpecificQueries)
                {
                    combinedQuery.Add(fieldQuery, BooleanClause.Occur.MUST);
                }

                generalQuery = combinedQuery;
            }

            return generalQuery;
        }

        private static Query BuildGeneralQuery(
            bool doExactId,
            string originalSearchText,
            Analyzer analyzer,
            IEnumerable<NuGetSearchTerm> generalTerms, 
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

            string escapedExactId = originalSearchText.ToLowerInvariant();

            Query exactIdQuery = null;
            Query wildCardIdQuery = null;
            if (doExactId)
            {
                exactIdQuery = new TermQuery(new Term("Id-Exact", escapedExactId));
                exactIdQuery.SetBoost(7.5f);

                wildCardIdQuery = new WildcardQuery(new Term("Id-Exact", "*" + escapedExactId + "*"));
            }

            Query nearlyExactIdQuery = null;
            if (generalTerms.Any())
            {
                string escapedApproximateId = string.Join(" ", generalTerms.Select(c => c.TermOrPhrase));
                nearlyExactIdQuery = AnalysisHelper.GetFieldQuery(analyzer, "Id", escapedApproximateId);
                nearlyExactIdQuery.SetBoost(2.0f);
            }

            foreach (var termQuery in generalQueries)
            {
                conjuctionQuery.Add(termQuery, BooleanClause.Occur.MUST);
                disjunctionQuery.Add(termQuery, BooleanClause.Occur.SHOULD);
            }

            var sanitizedTerms = generalTerms.Select(c => c.TermOrPhrase.ToLowerInvariant());
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
            var queries = new Query[]
            {
                exactIdQuery, wildCardIdQuery, nearlyExactIdQuery, conjuctionQuery, disjunctionQuery, wildCardQuery
            };

            var queriesToCombine = queries.Where(q => !IsDegenerateQuery(q));
            var query = conjuctionQuery.Combine(queriesToCombine.ToArray());
            return query;
        }
        
        // Helper function 
        // 1) fix cases of field names: ID -> Id
        // 2) null out field names that we don't understand (so we will search them as non-field-specific terms)
        // 3) For ID search, split search terms such as Id:"Foo.Bar" and "Foo-Bar" into a phrase "Foo Bar" which will work better for analyzed field search
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA1309:UseOrdinalStringComparison", MessageId = "System.String.Equals(System.String,System.StringComparison)")]
        private static NuGetSearchTerm StandardizeSearchTerms(NuGetSearchTerm input)
        {
            var fieldName = Fields
                .FirstOrDefault(f => f.Equals(input.Field, StringComparison.InvariantCultureIgnoreCase));

            var searchTerm = new NuGetSearchTerm { Field = fieldName, TermOrPhrase = input.TermOrPhrase };

            if (fieldName == "Id")
            {
                searchTerm.TermOrPhrase = LuceneIndexingService.SplitId(searchTerm.TermOrPhrase);
            }

            return searchTerm;
        }

        private static bool IsDegenerateQuery(Query q)
        {
            return q == null || (q is MatchAllDocsQuery) || ((q is BooleanQuery) && (q as BooleanQuery).Clauses().Count == 0);
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
    }
}