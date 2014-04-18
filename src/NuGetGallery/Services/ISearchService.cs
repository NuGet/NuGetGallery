using System;
using System.Linq;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public interface ISearchService
    {
        /// <summary>
        ///     Searches for packages that match the search filter and returns a set of results.
        /// </summary>
        /// <param name="filter"> The filter to be used. </param>
        /// <returns>The number of hits in the search and, if the CountOnly flag in SearchFilter was false, the results themselves</returns>
        Task<SearchResults> Search(SearchFilter filter);

        /// <summary>
        /// Gets a boolean indicating if all versions of each package are stored in the index
        /// </summary>
        bool ContainsAllVersions { get; }
    }

    public interface IRawSearchService
    {
        /// <summary>
        /// Executes a raw lucene query against the search index
        /// </summary>
        /// <param name="filter">The query to execute, with the search term interpreted as a raw lucene query</param>
        /// <returns>The results of the query</returns>
        Task<SearchResults> RawSearch(SearchFilter filter);
    }

    public class SearchResults
    {
        public int Hits { get; private set; }
        public DateTime? IndexTimestampUtc { get; private set; }
        public IQueryable<Package> Data { get; private set; }

        public SearchResults(int hits, DateTime? indexTimestampUtc)
            : this(hits, indexTimestampUtc, Enumerable.Empty<Package>().AsQueryable())
        {
        }

        public SearchResults(int hits, DateTime? indexTimestampUtc, IQueryable<Package> data)
        {
            Hits = hits;
            Data = data;
            IndexTimestampUtc = indexTimestampUtc;
        }
    }
}