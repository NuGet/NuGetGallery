using System.Linq;

namespace NuGetGallery
{
    public interface ISearchService
    {
        /// <summary>
        /// Searches for packages that match the search filter and returns a set of results.
        /// </summary>
        /// <param name="packages">A query representing the packages to be searched for.</param>
        /// <param name="filter">The filter to be used.</param>
        /// <param name="totalHits">The total number of packages discovered.</param>
        IQueryable<Package> Search(IQueryable<Package> packages, SearchFilter filter, out int totalHits);
    }
}