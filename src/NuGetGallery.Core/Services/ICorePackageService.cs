// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using NuGetGallery.Packaging;

namespace NuGetGallery
{
    /// <summary>
    /// Package business logic that is needed in multiple components (not just the Gallery process).
    /// </summary>
    public interface ICorePackageService<T> where T :IEntity
    {
        /// <summary>
        /// Updates the package properties related to the package stream itself.
        /// </summary>
        /// <param name="package">The package to update the stream details of.</param>
        /// <param name="metadata">The new package stream metadata.</param>
        /// <param name="commitChanges">Whether or not to commit the changes to the entity context.</param>
        Task UpdatePackageStreamMetadataAsync(T package, PackageStreamMetadata metadata, bool commitChanges = true);

        /// <summary>
        /// Set the status on the package and any other related package properties.
        /// </summary>
        /// <param name="package">The package to update the status of.</param>
        /// <param name="newPackageStatus">The new package status.</param>
        /// <param name="commitChanges">Whether or not to commit the changes to the entity context.</param>
        Task UpdatePackageStatusAsync(T package, PackageStatus newPackageStatus, bool commitChanges = true);

        /// <summary>
        /// Gets the package with the given ID and version when exists; otherwise <c>null</c>.
        /// </summary>
        /// <param name="id">The package ID.</param>
        /// <param name="version">The package version.</param>
        T FindPackageByIdAndVersionStrict(string id, string version);

        /// <summary>
        /// Updates the <see cref="Package.IsLatest"/> and related properties on all packages in the provided
        /// <see cref="PackageRegistration"/>. The <see cref="PackageRegistration.Packages"/> collection must be
        /// populated for this method to take effect.
        /// </summary>
        /// <param name="packageRegistration">The package registration.</param>
        /// <param name="commitChanges">Whether or not to commit the changes to the packages.</param>
        Task UpdateIsLatestAsync(PackageRegistration packageRegistration, bool commitChanges = true);

        PackageStatus GetStatus(T package);

    }
}