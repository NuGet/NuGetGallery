// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using NuGet.Services.Entities;
using NuGetGallery.Packaging;

namespace NuGetGallery
{
    /// <summary>
    /// Package business logic that is needed in multiple components (not just the Gallery process).
    /// </summary>
    public interface ICorePackageService
    {
        /// <summary>
        /// Updates the package properties related to the package stream itself.
        /// </summary>
        /// <param name="package">The package to update the stream details of.</param>
        /// <param name="metadata">The new package stream metadata.</param>
        /// <param name="commitChanges">Whether or not to commit the changes to the entity context.</param>
        Task UpdatePackageStreamMetadataAsync(Package package, PackageStreamMetadata metadata, bool commitChanges = true);

        /// <summary>
        /// Set the status on the package and any other related package properties.
        /// </summary>
        /// <param name="package">The package to update the status of.</param>
        /// <param name="newPackageStatus">The new package status.</param>
        /// <param name="commitChanges">Whether or not to commit the changes to the entity context.</param>
        Task UpdatePackageStatusAsync(Package package, PackageStatus newPackageStatus, bool commitChanges = true);

        /// <summary>
        /// Gets the package with the given ID and version when exists; otherwise <c>null</c>. Note that this method
        /// can return a soft deleted package. This will be indicated by <see cref="PackageStatus.Deleted"/> on the
        /// <see cref="Package.PackageStatusKey"/> property. Hard deleted packages will be returned as null because no
        /// record of the package exists. Consider checking for a null package and a soft deleted depending on your
        /// desired behavior for non-existent and deleted packages.
        /// </summary>
        /// <param name="id">The package ID.</param>
        /// <param name="version">The package version.</param>
        Package FindPackageByIdAndVersionStrict(string id, string version);

        /// <summary>
        /// Updates the <see cref="Package.IsLatest"/> and related properties on all packages in the provided
        /// <see cref="PackageRegistration"/>. The <see cref="PackageRegistration.Packages"/> collection must be
        /// populated for this method to take effect.
        /// </summary>
        /// <param name="packageRegistration">The package registration.</param>
        /// <param name="commitChanges">Whether or not to commit the changes to the packages.</param>
        Task UpdateIsLatestAsync(PackageRegistration packageRegistration, bool commitChanges = true);

        /// <summary>
        /// Updates the <see cref="Package.CertificateKey"/>.
        /// </summary>
        /// <param name="packageId">The package key.</param>
        /// <param name="packageVersion">The package key.</param>
        /// <param name="thumbprint">The certificate thumbprint.</param>
        /// <exception cref="ArgumentException">Thrown if <paramref name="packageId" /> is <c>null</c> or empty.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="packageVersion" /> is <c>null</c> or empty.</exception>
        /// <exception cref="ArgumentException">Thrown if the package does not exist.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="thumbprint" /> is <c>null</c> or empty
        /// or a certificate with the specified thumbprint does not exist.</exception>
        Task UpdatePackageSigningCertificateAsync(string packageId, string packageVersion, string thumbprint);

        /// <summary>
        /// Gets the package registration with the specified ID when it exists; otherwise, <c>null</c>.
        /// </summary>
        /// <param name="packageId">The package ID.</param>
        /// <returns>A package registration or <c>null</c>.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="packageId" /> is <c>null</c>
        /// or empty.</exception>
        PackageRegistration FindPackageRegistrationById(string packageId);
    }
}