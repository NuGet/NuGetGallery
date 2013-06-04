using System.Collections.Generic;

namespace NuGetGallery
{
    public class EditPackageRequest
    {
        public EditPackageRegistrationRequest EditPackageRegistrationRequest { get; set; }
        public EditPackageVersionRequest EditPackageVersionRequest { get; set; }

        public string PackageId { get; set; }
        public string PackageTitle { get; set; }
        public string Version { get; set; }

        public IList<Package> PackageVersions { get; set; }
    }
}