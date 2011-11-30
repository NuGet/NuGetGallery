using System.Collections.Generic;
using System.Linq;

namespace NuGetGallery
{
    public class PackageSearchResults
    {
        public IQueryable<Package> Packages { get; set; }

        public IEnumerable<int> RankedKeys { get; set; }
    }
}