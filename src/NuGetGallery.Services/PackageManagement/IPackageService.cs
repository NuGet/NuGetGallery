// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Packaging;
using NuGet.Services.Entities;
using NuGetGallery.Packaging;

namespace NuGetGallery
{
    /// <summary>
    /// Business logic related to <see cref="Package"/> and <see cref="PackageRegistration"/> instances. This logic is
    /// only used by the NuGet gallery, as opposed to the <see cref="CorePackageService"/> which is intended for other
    /// components.
    /// </summary>
    public interface IPackageService : ICorePackageService
    {
        /// <summary>
        /// Returns all packages with an <see cref="Package.Id"/> of <paramref name="id"/>.
        /// Includes deprecation fields based on <paramref name="deprecationFields"/>.
        /// </summary>
        IReadOnlyCollection<Package> FindPackagesById(
            string id, 
            PackageDeprecationFieldsToInclude deprecationFields = PackageDeprecationFieldsToInclude.None);

        /// <summary>
        /// Returns all packages with an <see cref="Package.Id"/> of <paramref name="id"/>.
        /// Includes the <see cref="Package.PackageRegistration"/> fields based on <paramref name="includePackageRegistration"/>.
        /// </summary>
        IReadOnlyCollection<Package> FindPackagesById(string id, bool includePackageRegistration);

        /// <summary>
        /// Gets the package with the given ID and version when exists;
        /// otherwise gets the latest package version for the given package ID matching the provided constraints.
        /// </summary>
        /// <param name="id">The package ID.</param>
        /// <param name="version">The package version if known; otherwise <c>null</c> to fallback to retrieve the latest version matching filter criteria.</param>
        /// <param name="semVerLevelKey">The SemVer-level key that determines the SemVer filter to be applied.</param>
        /// <param name="allowPrerelease"><c>True</c> indicating pre-release packages are allowed, otherwise <c>false</c>.</param>
        /// <returns></returns>
        Package FindPackageByIdAndVersion(
            string id,
            string version,
            int? semVerLevelKey = null,
            bool allowPrerelease = true);

        /// <summary>
        /// Filters a list of packages for an exact version.
        /// </summary>
        /// <param name="packages">The list of packages to search.</param>
        /// <param name="version">The version to search for. This version is normalized and compared case-insensitively.</param>
        /// <returns>
        /// Returns the package in <paramref name="packages"/> with the exact version.
        /// Returns null if no such package exists.
        /// </returns>
        Package FilterExactPackage(
            IReadOnlyCollection<Package> packages,
            string version);

        /// <summary>
        /// Filters a list of packages and returns the latest that follows some criteria.
        /// </summary>
        /// <param name="packages">The list of packages to search.</param>
        /// <param name="semVerLevelKey">The SemVer-level key that determines the SemVer filter to be applied.</param>
        /// <param name="allowPrerelease"><c>True</c> indicating pre-release packages are allowed, otherwise <c>false</c>.</param>
        /// <returns>
        /// Returns the latest package in <paramref name="packages"/> that follows the criteria.
        /// Returns null if no such package exists.
        /// </returns>
        Package FilterLatestPackage(
            IReadOnlyCollection<Package> packages,
            int? semVerLevelKey = SemVerLevelKey.SemVer2,
            bool allowPrerelease = true);

        IEnumerable<Package> FindPackagesByOwner(User user, bool includeUnlisted, bool includeVersions = false);

        IEnumerable<Package> FindPackagesByAnyMatchingOwner(User user, bool includeUnlisted, bool includeVersions = false);

        IQueryable<PackageRegistration> FindPackageRegistrationsByOwner(User user);

        IQueryable<PackageRegistration> GetAllPackageRegistrations();

        IEnumerable<Package> FindDependentPackages(Package package);

        /// <summary>
        /// Populate the related database tables to create the specified package for the specified user. It is the
        /// caller's responsibility to commit changes to the entity context.
        /// </summary>
        /// <remarks>
        /// This method doesn't upload the package binary to the blob storage. The caller must do it after this call.
        /// </remarks>
        /// <param name="nugetPackage">The package to be created.</param>
        /// <param name="packageStreamMetadata">The package stream's metadata.</param>
        /// <param name="owner">The owner of the package</param>
        /// <param name="currentUser">The user that pushed the package on behalf of <paramref name="owner"/></param>
        /// <param name="isVerified">Mark the package registration as verified or not</param>
        /// <returns>The created package entity.</returns>
        Task<Package> CreatePackageAsync(PackageArchiveReader nugetPackage, PackageStreamMetadata packageStreamMetadata, User owner, User currentUser, bool isVerified);

        Package EnrichPackageFromNuGetPackage(Package package, PackageArchiveReader packageArchive, PackageMetadata packageMetadata, PackageStreamMetadata packageStreamMetadata, User user);

        Task PublishPackageAsync(string id, string version, bool commitChanges = true);
        Task PublishPackageAsync(Package package, bool commitChanges = true);

        /// <summary>
        /// Performs database changes to add a new package owner while removing the corresponding package owner request.
        /// </summary>
        /// <param name="package">Package to which owner is added.</param>
        /// <param name="newOwner">New owner to add.</param>
        /// <returns>Awaitable task.</returns>
        Task AddPackageOwnerAsync(PackageRegistration package, User newOwner, bool commitChanges = true);

        Task RemovePackageOwnerAsync(PackageRegistration package, User user, bool commitChanges = true);

        /// <remarks>
        /// A package is orphaned if it is not owned by a user account or an organization with user account members.
        /// </remarks>
        bool WillPackageBeOrphanedIfOwnerRemoved(PackageRegistration package, User ownerToRemove);

        Task SetLicenseReportVisibilityAsync(Package package, bool visible, bool commitChanges = true);

        Task EnsureValid(PackageArchiveReader packageArchiveReader);

        Task IncrementDownloadCountAsync(string id, string version, bool commitChanges = true);

        Task UpdatePackageVerifiedStatusAsync(IReadOnlyCollection<PackageRegistration> package, bool isVerified, bool commitChanges = true);

        /// <summary>
        /// Sets the required signer on all owned package registrations.
        /// </summary>
        /// <param name="signer">A signer or <c>null</c> if none.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="signer" />
        /// is <c>null</c>.</exception>
        Task SetRequiredSignerAsync(User signer);

        /// <summary>
        /// Sets the required signer on an owned package registration.
        /// </summary>
        /// <param name="registration">A package registration.</param>
        /// <param name="signer">A signer or <c>null</c> if none.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="registration" />
        /// is <c>null</c>.</exception>
        Task SetRequiredSignerAsync(PackageRegistration registration, User signer, bool commitChanges = true);
    }
}