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
        Package FindPackageByIdAndVersion(string id, string version, bool allowPrerelease = true);
        IEnumerable<Package> FindPackagesByOwner(User user, bool includeUnlisted);
        IEnumerable<PackageRegistration> FindPackageRegistrationsByOwner(User user);
        IEnumerable<Package> FindDependentPackages(Package package);

        /// <summary>
        /// Updates IsLatest/IsLatestStable flags after a package create, update or delete operation.
        /// 
        /// Optimistic concurrency was added to this update to prevent multiple threads (in the same
        /// or different gallery instance) from setting the IsLatest/IsLatestStable flag to true on
        /// different package versions. The concurrency check is manual, avoiding EF's ConcurrencyCheck
        /// attribute, because we only want to reject concurrent updates to the IsLatest/IsLatestStable
        /// columns and not package deletes or updates of other columns.
        /// 
        /// When concurrency is detected, UpdateIsLatest will fetch the latest from the database and
        /// retry just in case the concurrent update didn't have the latest. More than likely the other
        /// update did have the latest since package updates are committed in a separate transaction,
        /// and the retry will detect that no changes are necessary.
        /// 
        /// Since EF contexts are short-lived and do not really support refresh, UpdateIsLatest will
        /// use a different context than the request when retrying to avoid putting the request context
        /// in a bad state. The request context will be updated with the original (non-retry)
        /// IsLatest/IsLatestStable values for use by the remainder of the request. These updates should
        /// not be committed, therefore UpdateIsLatestAsync should only be called after all other commits
        /// are complete.
        /// </summary>
        /// <param name="packageRegistration"></param>
        /// <returns></returns>
        Task UpdateIsLatestAsync(PackageRegistration packageRegistration);

        /// <summary>
        /// Populate the related database tables to create the specified package for the specified user.
        /// </summary>
        /// <remarks>
        /// This method doesn't upload the package binary to the blob storage. The caller must do it after this call.
        /// This method doesn't update IsLatest/IsLatestStable flags. The caller must do it after this call.
        /// </remarks>
        /// <param name="nugetPackage">The package to be created.</param>
        /// <param name="packageStreamMetadata">The package stream's metadata.</param>
        /// <param name="user">The owner of the package</param>
        /// <param name="commitChanges">Specifies whether to commit the changes to database.</param>
        /// <returns>The created package entity.</returns>
        Task<Package> CreatePackageAsync(PackageArchiveReader nugetPackage, PackageStreamMetadata packageStreamMetadata, User user, bool commitChanges = true);

        Package EnrichPackageFromNuGetPackage(Package package, PackageArchiveReader packageArchive, PackageMetadata packageMetadata, PackageStreamMetadata packageStreamMetadata, User user);

        /// <summary>
        /// Publishes a package by listing it.
        /// </summary>
        /// <remarks>
        /// This method doesn't update IsLatest/IsLatestStable flags. The caller must do it after this call.
        /// </remarks>
        /// <param name="id">ID for the package to publish</param>
        /// <param name="version">Package version</param>
        /// <param name="commitChanges">Whether to commit changes to the database.</param>
        /// <returns></returns>
        Task PublishPackageAsync(string id, string version, bool commitChanges = true);

        /// <summary>
        /// Publishes a package by listing it.
        /// </summary>
        /// <remarks>
        /// This method doesn't update IsLatest/IsLatestStable flags. The caller must do it after this call.
        /// </remarks>
        /// <param name="package">The package to publish</param>
        /// <param name="commitChanges">Whether to commit changes to the database.</param>
        /// <returns></returns>
        Task PublishPackageAsync(Package package, bool commitChanges = true);

        /// <summary>
        /// Mark a package as unlisted.
        /// </summary>
        /// <remarks>
        /// This method doesn't update IsLatest/IsLatestStable flags. The caller must do it after this call.
        /// </remarks>
        /// <param name="package">The package to unlist</param>
        /// <param name="commitChanges">Whether to commit changes to the database.</param>
        /// <returns></returns>
        Task MarkPackageUnlistedAsync(Package package, bool commitChanges = true);

        /// <summary>
        /// Mark a package as listed.
        /// </summary>
        /// <remarks>
        /// This method doesn't update IsLatest/IsLatestStable flags. The caller must do it after this call.
        /// </remarks>
        /// <param name="package">The package to list.</param>
        /// <param name="commitChanges">Whether to commit changes to the database.</param>
        /// <returns></returns>
        Task MarkPackageListedAsync(Package package, bool commitChanges = true);

        Task<PackageOwnerRequest> CreatePackageOwnerRequestAsync(PackageRegistration package, User currentOwner, User newOwner);
        Task<ConfirmOwnershipResult> ConfirmPackageOwnerAsync(PackageRegistration package, User user, string token);
        Task AddPackageOwnerAsync(PackageRegistration package, User user);
        Task RemovePackageOwnerAsync(PackageRegistration package, User user);

        Task SetLicenseReportVisibilityAsync(Package package, bool visible, bool commitChanges = true);

        void EnsureValid(PackageArchiveReader packageArchiveReader);

        Task IncrementDownloadCountAsync(string id, string version, bool commitChanges = true);
    }
}