// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using NuGet.Packaging;
using NuGet.Services.Entities;
using NuGetGallery.Packaging;

namespace NuGetGallery
{
    public interface IPackageMetadataValidationService
    {
        /// <summary>
        /// Validate the provided package archive reader before generating package
        /// This is useful for finding errors or warnings that should be caught before the user verifies their
        /// UI package upload.
        /// </summary>
        /// <param name="nuGetPackage">The package archive reader.</param>
        /// <param name="packageMetadata">The package metadata.</param>
        /// <param name="currentUser">The user who pushed the package.</param>
        /// <returns>The package validation result.</returns>
        Task<PackageValidationResult> ValidateMetadataBeforeUploadAsync(
            PackageArchiveReader nuGetPackage,
            PackageMetadata packageMetadata,
            User currentUser);

        /// <summary>
        /// Validate the provided package once the new packages owner is known. The validations performed by this
        /// method should make sense for both the UI upload and command line push. This should be called after
        /// generating package
        /// </summary>
        /// <param name="package">The package entity not yet committed to the database.</param>
        /// <param name="nuGetPackage">The package archive reader.</param>
        /// <param name="owner">The owner of the package.</param>
        /// <param name="currentUser">The current user.</param>
        /// <param name="isNewPackageRegistration">Determine whether the uploaded package is a new package without existing package registration info.</param>
        /// <returns>The package validation result.</returns>
        Task<PackageValidationResult> ValidateMetadaAfterGeneratePackageAsync(
            Package package,
            PackageArchiveReader nuGetPackage,
            User owner,
            User currentUser,
            bool isNewPackageRegistration);
    }
}
