using System.Collections.Generic;
using NuGet;

namespace NuGetGallery
{
    public interface IPackageService
    {
        Package CreatePackage(
            ZipPackage zipPackage,
            User currentUser);

        Package FindByIdAndVersion(
            string id, 
            string version = null);

        IEnumerable<Package> GetLatestVersionOfPublishedPackages();

        void PublishPackage(Package package);
    }
}