using System;
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
                Dependencies = new DependencySetsViewModel(package.Dependencies);
                PackageVersions = from p in package.PackageRegistration.Packages.ToList()
                                  orderby new SemanticVersion(p.Version) descending
                                  select new DisplayPackageViewModel(p, isVersionHistory: true);
            }
            DownloadCount = package.DownloadCount;
        }

        public void SetPendingMetadata(PackageEdit pendingMetadata)
        {
            if (pendingMetadata.TriedCount < 3)
            {
                HasPendingMetadata = true;
                Authors = pendingMetadata.Authors;
                Copyright = pendingMetadata.Copyright;
                Description = pendingMetadata.Description;
                IconUrl = pendingMetadata.IconUrl;
                LicenseUrl = pendingMetadata.LicenseUrl;
                ProjectUrl = pendingMetadata.ProjectUrl;
                ReleaseNotes = pendingMetadata.ReleaseNotes;
                Tags = pendingMetadata.Tags.ToStringSafe().Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
                Title = pendingMetadata.Title;
            }
            else
            {
                HasPendingMetadata = false;
                IsLastEditFailed = true;
            }
        }

        public DependencySetsViewModel Dependencies { get; set; }
        public IEnumerable<DisplayPackageViewModel> PackageVersions { get; set; }
        public string Copyright { get; set; }

        public bool HasPendingMetadata { get; private set; }
        public bool IsLastEditFailed { get; private set; }

        public bool HasNewerPrerelease
        {
            get
            {
                return PackageVersions.Any(pv => pv.LatestVersion && !pv.LatestStableVersion);
            }
        }
    }
}