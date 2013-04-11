using System.Collections.Generic;

namespace NuGetGallery
{
    public class UserProfileModel
    {
        public string Username { get; set; }
        public string EmailAddress { get; set; }
        public ICollection<PackageViewModel> OwnedPackages { get; set; }
        public ICollection<PackageViewModel> FollowedPackages { get; set; }
        public int TotalPackageDownloadCount { get; set; }
    }
}