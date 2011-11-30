using System.Collections.Generic;
using System.Linq;

namespace NuGetGallery
{
    public interface ISearchService
    {
        IQueryable<Package> Search(IQueryable<Package> packages, string searchTerm);

        IQueryable<Package> SearchWithRelevance(IQueryable<Package> packages, string searchTerm);

        IQueryable<Package> SearchWithRelevance(IQueryable<Package> packages, string searchTerm, int take, out int totalHits);
    }
}