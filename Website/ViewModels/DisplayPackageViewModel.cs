using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;

namespace NuGetGallery {
    public class DisplayPackageViewModel : PackageViewModel {
        public DisplayPackageViewModel(Package package)
            : this(package, true) {
        }

        public DisplayPackageViewModel(Package package, bool includePackageVersions)
            : base(package) {

            RatingCount = package.Reviews.Count;
            RatingSum = package.Reviews.Sum(r => r.Rating);
            Tags = package.Tags != null ? package.Tags.Trim().Split(' ') : null;
            if (includePackageVersions) {
                Authors = package.Authors;
                Dependencies = package.Dependencies.Select(d => new DependencyViewModel(d));
                Owners = package.PackageRegistration.Owners;
                PackageVersions = from p in package.PackageRegistration.Packages
                                  orderby p.Version descending
                                  select new DisplayPackageViewModel(p, false);
            }
        }
        public IEnumerable<PackageAuthor> Authors { get; set; }
        public IEnumerable<DependencyViewModel> Dependencies { get; set; }
        public ICollection<User> Owners { get; set; }
        public IEnumerable<string> Tags { get; set; }
        public int TotalDownloadCount { get; set; }
        public int RatingCount { get; set; }
        public int RatingSum { get; set; }
        public float RatingAverage {
            get {
                if (RatingCount > 0) {
                    return (float)RatingSum / (float)RatingCount;
                }
                return 0;
            }
        }

        public IEnumerable<DisplayPackageViewModel> PackageVersions { get; set; }

        public bool IsOwner(IPrincipal user) {
            if (user == null || user.Identity == null) {
                return false;
            }
            return Owners.Any(u => u.Username == user.Identity.Name);
        }

        public bool IsCurrent(DisplayPackageViewModel current) {
            return current.Version == Version && current.Id == Id;
        }
    }
}