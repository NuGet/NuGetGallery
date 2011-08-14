using System.Collections.Generic;

namespace NuGetGallery {
    public class ManagePackagesViewModel {
        public IEnumerable<PackageViewModel> UnpublishedPackages { get; set; }
        public IEnumerable<PackageViewModel> PublishedPackages { get; set; }
    }
}