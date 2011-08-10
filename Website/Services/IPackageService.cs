using System.Collections.Generic;
using NuGet;

namespace NuGetGallery {
    public interface IPackageService {
        Package CreatePackage(
            IPackage nugetPackage,
            User currentUser);

<<<<<<< HEAD
        Package FindByIdAndVersion(
            string id,
=======
        PackageRegistration FindPackageRegistrationById(string id);
        
        Package FindPackageByIdAndVersion(
            string id, 
>>>>>>> refactored, fixed (added missing data), and added tests for PackageService.CreatePackage
            string version = null);

        IEnumerable<Package> GetLatestVersionOfPublishedPackages();

        void PublishPackage(Package package);
    }
}