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
    }

    public class SearchResults
    {
        public int Hits { get; private set; }
        public IQueryable<Package> Data { get; private set; }

        public SearchResults(int hits)
            : this(hits, Enumerable.Empty<Package>().AsQueryable())
        {
        }

        public SearchResults(int hits, IQueryable<Package> data)
        {
            Hits = hits;
            Data = data;
        }
    }
}