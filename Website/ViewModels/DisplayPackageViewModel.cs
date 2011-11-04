using System.Collections.Generic;
using System.Linq;
using NuGet;

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
            Copyright = package.Copyright;
            if (!isVersionHistory)
            {
                Dependencies = package.Dependencies.Select(d => new DependencyViewModel(d));
                PackageVersions = from p in package.PackageRegistration.Packages.ToList()
                                  orderby new SemanticVersion(p.Version) descending
                                  select new DisplayPackageViewModel(p, isVersionHistory: true);
            }

            DownloadCount = package.DownloadCount;
        }

        public IEnumerable<DependencyViewModel> Dependencies { get; set; }
        public IEnumerable<DisplayPackageViewModel> PackageVersions { get; set; }
        public string Copyright { get; set; }

        public bool IsLatestVersionAvailable
        {
            get
            {
                // A package can be identified as the latest available a few different ways
                // First, if it's marked as the latest stable version
                return this.LatestStableVersion
                    // Or if it's marked as the latest version (pre-release)
                    || this.LatestVersion
                    // Or if it's the current version and no version is marked as the latest (because they're all unlisted)
                    || (this.IsCurrent(this) && !this.PackageVersions.Any(p => p.LatestVersion));
            }
        }
    }
}