using System.Collections.Generic;

namespace NuGetGallery
{
    public class ListPackageItemViewModel : PackageViewModel
    {
        public ListPackageItemViewModel(Package package, bool needAuthors = true)
            : base(package)
        {
            Tags = package.Tags != null ? package.Tags.Trim().Split(' ') : null;

            if (needAuthors)
            {
                Authors = package.Authors;
            }

            MinClientVersion = package.MinClientVersion;
        }

        public IEnumerable<PackageAuthor> Authors { get; set; }
        public IEnumerable<string> Tags { get; set; }
        public string MinClientVersion { get; set; }

        public bool UseVersion
        {
            get
            {
                // only show the version when we'll end up listing the package more than once. This would happen when the latest version is not the same as the latest stable version.
                return !(LatestVersion && LatestStableVersion);
            }
        }
    }
}
