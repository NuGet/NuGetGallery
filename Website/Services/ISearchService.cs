using System.Linq;

namespace NuGetGallery
{
    public interface ISearchService
    {
        /// <summary>
        ///     Searches for packages that match the search filter and returns a set of results.
        /// </summary>
        /// <param name="filter"> The filter to be used. </param>
        /// <param name="totalHits"> The total number of packages discovered. </param>
        /// <param name="filterToPackageSet"> Optional: A subset of packages to restrict search results to. </param>
        IQueryable<Package> Search(SearchFilter filter, out int totalHits, IQueryable<Package> filterToPackageSet = null);
    }
}