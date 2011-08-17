using System.Collections.Generic;
using System.Linq;

namespace NuGetGallery {
    public class DeletePackageViewModel {
        public DeletePackageViewModel(Package package, IEnumerable<Package> dependentPackages) {
            Package = new ListPackageItemViewModel(package);
            DependentPackages = dependentPackages.Select(p => new PackageViewModel(p));
            Published = package.Published != null;
            MayDelete = !Published || (!DependentPackages.Any() && package.DownloadCount < 5);
        }
        public ListPackageItemViewModel Package { get; private set; }
        public IEnumerable<IPackageVersionModel> DependentPackages { get; private set; }
        public bool MayDelete { get; private set; }
        public bool Published { get; private set; }
    }
}