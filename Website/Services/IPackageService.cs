using System.Collections.Generic;
using NuGet;

namespace NuGetGallery {
    public interface IPackageService {
        Package CreatePackage(
            IPackage nugetPackage,
            User currentUser);

        void DeletePackage(string id, string version);  

        PackageRegistration FindPackageRegistrationById(string id);

        Package FindPackageByIdAndVersion(
            string id,
            string version = null);

        IEnumerable<Package> GetLatestVersionOfPublishedPackages();

        void PublishPackage(
            string id,
            string version);
    }
}