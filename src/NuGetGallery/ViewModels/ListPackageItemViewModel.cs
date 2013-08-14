using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;

namespace NuGetGallery
{
    public class ListPackageItemViewModel : PackageViewModel
    {
        public ListPackageItemViewModel(Package package)
            : base(package)
        {
            Tags = package.Tags != null ? package.Tags.Trim().Split(' ') : null;

            Authors = package.FlattenedAuthors;
            MinClientVersion = package.MinClientVersion;
            Owners = package.PackageRegistration.Owners;
        }

        public string Authors { get; set; }
        public ICollection<User> Owners { get; set; }
        public IEnumerable<string> Tags { get; set; }
        public string MinClientVersion { get; set; }

        public bool UseVersion
        {
            get
            {
                // only use the version in URLs when necessary. This would happen when the latest version is not the same as the latest stable version.
                return !(LatestVersion && LatestStableVersion);
            }
        }

        public bool IsOwner(IPrincipal user)
        {
            if (user == null || user.Identity == null)
            {
                return false;
            }
            return user.IsAdministrator() || Owners.Any(u => u.Username == user.Identity.Name);
        }
    }
}