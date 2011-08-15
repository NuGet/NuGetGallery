using System.Collections.Generic;
using System.Linq;

namespace NuGetGallery {
    public class DisplayPackageViewModel : ListPackageItemViewModel {
        public DisplayPackageViewModel(Package package)
            : this(package, false) {
        }

        public DisplayPackageViewModel(Package package, bool isVersionHistory)
            : base(package) {

            RatingCount = package.Reviews.Count;
            RatingSum = package.Reviews.Sum(r => r.Rating);

            if (!isVersionHistory) {
                Dependencies = package.Dependencies.Select(d => new DependencyViewModel(d));
                PackageVersions = from p in package.PackageRegistration.Packages
                                  orderby p.Version descending
                                  select new DisplayPackageViewModel(p, isVersionHistory: true);
            }
        }
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
        public IEnumerable<DependencyViewModel> Dependencies { get; set; }
        public IEnumerable<DisplayPackageViewModel> PackageVersions { get; set; }
    }
}