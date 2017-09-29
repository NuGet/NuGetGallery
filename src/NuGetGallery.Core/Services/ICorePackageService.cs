// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace NuGetGallery
{
    /// <summary>
    /// Package business logic that is needed in multiple components (not just the Gallery process).
    /// </summary>
    public interface ICorePackageService
    {
        /// <summary>
        /// Set the status on the package and any other related package properties.
        /// </summary>
        /// <param name="package">The package to update the status of.</param>
        /// <param name="newPackageStatus">The new package status.</param>
        /// <param name="commitChanges">Whether or not to commit the changes to the entity context.</param>
        Task UpdatePackageStatusAsync(Package package, PackageStatus newPackageStatus, bool commitChanges = true);

        /// <summary>
        /// Gets the package with the given ID and version when exists; otherwise <c>null</c>.
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
    }
}