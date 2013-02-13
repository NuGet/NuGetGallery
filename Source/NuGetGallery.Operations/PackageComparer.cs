using System;
using System.Collections.Generic;

namespace NuGetGallery.Operations
{
    public class PackageComparer : IEqualityComparer<Package>
    {
        public bool Equals(
            Package firstPackage, 
            Package secondPackage)
        {
            return firstPackage.Id.Equals(secondPackage.Id, StringComparison.OrdinalIgnoreCase) &&
                firstPackage.Version.Equals(secondPackage.Version, StringComparison.OrdinalIgnoreCase) &&
                firstPackage.Hash.Equals(secondPackage.Hash, StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode(Package package)
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 23 + package.Id.GetHashCode();
                hash = hash * 23 + package.Version.GetHashCode();
                hash = hash * 23 + package.Hash.GetHashCode();
                return hash;
            }
        }
    }
}
