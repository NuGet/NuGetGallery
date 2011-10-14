using System.Collections.Generic;
using System.Linq;

namespace NuGetGallery
{
    public class DisplayPackageViewModel : ListPackageItemViewModel
    {
        public DisplayPackageViewModel(Package package)
            : this(package, false)
        {
        }

        public DisplayPackageViewModel(Package package, bool isVersionHistory)
            : base(package)
        {

            if (!isVersionHistory)
            {
                Dependencies = package.Dependencies.Select(d => new DependencyViewModel(d));
                PackageVersions = from p in package.PackageRegistration.Packages
                                  orderby p.Version descending
                                  select new DisplayPackageViewModel(p, isVersionHistory: true);
            }

            DownloadCount = package.DownloadStatistics.Count;
        }

        public IEnumerable<DependencyViewModel> Dependencies { get; set; }
        public IEnumerable<DisplayPackageViewModel> PackageVersions { get; set; }
    }
}