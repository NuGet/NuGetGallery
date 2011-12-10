using System.Collections.Generic;

namespace NuGetGallery
{
    public class UserProfileModel
    {
        public string Username { get; set; }
        public string EmailAddress { get; set; }
        public ICollection<PackageViewModel> Packages { get; set; }
        public int TotalPackageDownloadCount { get; set; }
    }
}