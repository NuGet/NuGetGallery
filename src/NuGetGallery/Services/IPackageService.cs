// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using NuGet.Packaging;
using NuGetGallery.Packaging;

namespace NuGetGallery
{
    public interface IPackageService
    {
        PackageRegistration FindPackageRegistrationById(string id);

        /// <summary>
        /// Gets the package with the given ID and version when exists;
        /// otherwise gets the latest package version for the given package ID matching the provided constraints.
        /// </summary>
        /// <param name="id">The package ID.</param>
        /// <param name="version">The package version if known; otherwise <c>null</c> to fallback to retrieve the latest version matching filter criteria.</param>
        /// <param name="semVerLevelKey">The SemVer-level key that determines the SemVer filter to be applied.</param>
        /// <param name="allowPrerelease"><c>True</c> indicating pre-release packages are allowed, otherwise <c>false</c>.</param>
        /// <returns></returns>
        Package FindPackageByIdAndVersion(string id, string version, int? semVerLevelKey = null, bool allowPrerelease = true);

        /// <summary>
        /// Gets the package with the given ID and version when exists; otherwise <c>null</c>.
        /// </summary>
        /// <param name="id">The package ID.</param>
        /// <param name="version">The package version.</param>
        Package FindPackageByIdAndVersionStrict(string id, string version);

        Package FindAbsoluteLatestPackageById(string id, int? semVerLevelKey);
        IEnumerable<Package> FindPackagesByOwner(User user, bool includeUnlisted);
        IEnumerable<PackageRegistration> FindPackageRegistrationsByOwner(User user);
        IEnumerable<Package> FindDependentPackages(Package package);

        Task UpdateIsLatestAsync(PackageRegistration packageRegistration, bool commitChanges = true);

        /// <summary>
        /// Populate the related database tables to create the specified package for the specified user.
        /// </summary>
        /// <remarks>
        /// This method doesn't upload the package binary to the blob storage. The caller must do it after this call.
        /// </remarks>
        /// <param name="nugetPackage">The package to be created.</param>
        /// <param name="packageStreamMetadata">The package stream's metadata.</param>
        /// <param name="user">The owner of the package</param>
        /// <param name="commitChanges">Specifies whether to commit the changes to database.</param>
        /// <returns>The created package entity.</returns>
        Task<Package> CreatePackageAsync(PackageArchiveReader nugetPackage, PackageStreamMetadata packageStreamMetadata, User user, bool commitChanges = true);

        Package EnrichPackageFromNuGetPackage(Package package, PackageArchiveReader packageArchive, PackageMetadata packageMetadata, PackageStreamMetadata packageStreamMetadata, User user);

        Task PublishPackageAsync(string id, string version, bool commitChanges = true);
        Task PublishPackageAsync(Package package, bool commitChanges = true);

        Task MarkPackageUnlistedAsync(Package package, bool commitChanges = true);
        Task MarkPackageListedAsync(Package package, bool commitChanges = true);

        Task<PackageOwnerRequest> CreatePackageOwnerRequestAsync(PackageRegistration package, User currentOwner, User newOwner);
        
        /// <summary>
        /// Checks if the pending owner has a request for this package which matches the specified token.
        /// </summary>
        /// <param name="package">Package associated with the request.</param>
        /// <param name="pendingOwner">Pending owner for the request.</param>
        /// <param name="token">Token generated for the owner request.</param>
        /// <returns>True if valid, false otherwise.</returns>
        bool IsValidPackageOwnerRequest(PackageRegistration package, User pendingOwner, string token);

        /// <summary>
        /// Performs database changes to add a new package owner while removing the corresponding package owner request.
        /// </summary>
        /// <param name="package">Package to which owner is added.</param>
        /// <param name="newOwner">New owner to add.</param>
        /// <returns>Awaitable task.</returns>
        Task AddPackageOwnerAsync(PackageRegistration package, User newOwner);

        Task RemovePackageOwnerAsync(PackageRegistration package, User user);

        PackageOwnerRequest GetPackageOwnerRequestAsync(PackageRegistration package, User requestingUser, User pendingUser);

        Task SetLicenseReportVisibilityAsync(Package package, bool visible, bool commitChanges = true);

        void EnsureValid(PackageArchiveReader packageArchiveReader);

        Task IncrementDownloadCountAsync(string id, string version, bool commitChanges = true);
    }
}