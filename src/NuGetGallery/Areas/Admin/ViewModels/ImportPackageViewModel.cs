using System.Collections.Generic;
using System.Linq;
using NuGet;

namespace NuGetGallery.Areas.Admin.ViewModels
{
    /// <summary>
    /// View model for displaying page details to import package.
    /// </summary>
    public class ImportPackageViewModel : ImportSearchItemViewModel
    {
        private const int KilobyteConversion = 1024;

        /// <summary>
        /// Initializes a new instance of the <see cref="ImportPackageViewModel"/> class.
        /// </summary>
        /// <param name="package">The package.</param>
        public ImportPackageViewModel(NuGetService.V2FeedPackage package)
            : this(package, null, null)
        {
        }

        public bool Existing { get; set; }

        public long PackageSize { get; set; }

        public IEnumerable<ImportDependency> Dependencies { get; set; }

        public IEnumerable<ImportPackageViewModel> PackageVersions { get; set; }

        public string Copyright { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ImportPackageViewModel"/> class.
        /// </summary>
        /// <param name="package">The package.</param>
        /// <param name="packageVersions">The package versions.</param>
        /// <param name="existingVersions">The versions of the package that already exist on the server.</param>
        public ImportPackageViewModel(NuGetService.V2FeedPackage package, IEnumerable<NuGetService.V2FeedPackage> packageVersions, IEnumerable<string> existingVersions)
            : base(package)
        {
            Copyright = package.Copyright;
            DownloadCount = package.DownloadCount;
            var preExistingVersions = existingVersions ?? Enumerable.Empty<string>();

            if (packageVersions != null)
            {
                PackageVersions = from p in packageVersions
                                  orderby new SemanticVersion(p.Version) descending
                                  select new ImportPackageViewModel(p)
                                  {
                                      Existing = preExistingVersions.Contains(p.Version)
                                  };
            }

            var dependencies = new List<ImportDependency>();
            foreach (var pair in package.Dependencies.Split('|'))
            {
                var parts = pair.Split(':');
                var id = parts[0];
                var versionSpec = string.Empty;
                if (parts.Length > 1)
                {
                    versionSpec = string.Join(", ", parts.Where(p => !string.IsNullOrWhiteSpace(p)).Skip(1));
                }

                dependencies.Add(new ImportDependency(id, versionSpec));
            }

            Dependencies = dependencies;
            PackageSize = package.PackageSize != 0 ? package.PackageSize / KilobyteConversion : 0;
        }
        
        public bool IsLatestVersionAvailable
        {
            get
            {
                // A package can be identified as the latest available a few different ways
                // First, if it's marked as the latest stable version
                return LatestStableVersion
                       // Or if it's marked as the latest version (pre-release)
                       || LatestVersion
                       // Or if it's the current version and no version is marked as the latest (because they're all unlisted)
                       || (IsCurrent(this) && !PackageVersions.Any(p => p.LatestVersion));
            }
        }
    }
}