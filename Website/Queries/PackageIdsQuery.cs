using System.Collections.Generic;

namespace NuGetGallery
{
    public interface IPackageIdsQuery
    {
        IEnumerable<string> Execute(
            string partialId,
            bool? includePrerelease = false);
    }

    public class PackageIdsQuery : IPackageIdsQuery
    {
        private readonly ISearchService _searchService;

        public PackageIdsQuery(ISearchService searchService)
        {
            _searchService = searchService;
        }

        public IEnumerable<string> Execute(
            string partialId,
            bool? includePrerelease = false)
        {
            var searchFilter = new SearchFilter
            {
                SearchTerm = partialId,
                IncludePrerelease = includePrerelease ?? false,
                Take = 30,
                SortProperty = SortProperty.DownloadCount,
                SortDirection = SortDirection.Descending
            };

            return _searchService.FindPackagesById(searchFilter);
        }
    }
}