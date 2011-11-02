using System.Collections.Generic;
using System.Linq;
using NuGet;

namespace NuGetGallery
{
    public interface IPackageService
    {
        Package CreatePackage(IPackage nugetPackage, User currentUser);

        void DeletePackage(string id, string version);

        PackageRegistration FindPackageRegistrationById(string id);

        Package FindPackageByIdAndVersion(string id, string version, bool allowPrerelease = true);

        IQueryable<Package> GetLatestPackageVersions(bool allowPrerelease);

        void PublishPackage(string id, string version);

        IEnumerable<Package> FindPackagesByOwner(User user);

        IEnumerable<Package> FindDependentPackages(Package package);

        PackageOwnerRequest CreatePackageOwnerRequest(PackageRegistration package, User currentOwner, User newOwner);

        bool ConfirmPackageOwner(PackageRegistration package, User user, string token);

        void AddPackageOwner(PackageRegistration package, User user);

        void RemovePackageOwner(PackageRegistration package, User user);

        void AddDownloadStatistics(Package package, string userHostAddress, string userAgent);

        void MarkPackageUnlisted(Package package);

        void MarkPackageListed(Package package);
    }
}