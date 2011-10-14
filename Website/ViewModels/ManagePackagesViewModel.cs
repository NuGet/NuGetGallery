using System.Collections.Generic;

namespace NuGetGallery
{
    public class ManagePackagesViewModel
    {
        public IEnumerable<PackageViewModel> Packages { get; set; }
    }
}