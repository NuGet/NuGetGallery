using System.Collections.Generic;
using NuGet;

namespace NuGetGallery {
    public interface IPackageService {
        Package CreatePackage(
            IPackage nugetPackage,
            User currentUser);

        PackageRegistration FindPackageRegistrationById(string id);
        
        Package FindPackageByIdAndVersion(
            string id, 
            string version = null);

        IEnumerable<Package> GetLatestVersionOfPublishedPackages();

        void PublishPackage(Package package);
    }
}