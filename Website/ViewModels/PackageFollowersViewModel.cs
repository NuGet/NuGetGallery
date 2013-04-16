using System.Collections.Generic;

namespace NuGetGallery
{
    public class PackageFollowersViewModel
    {
        public string PackageId { get; set; }
        public string PackageTitle { get; set; }
        public IEnumerable<User> Followers { get; set; }
    }
}