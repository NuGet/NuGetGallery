// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Threading.Tasks;
using NuGet.Packaging;
using NuGet.Services.Entities;
using NuGetGallery.Packaging;

namespace NuGetGallery
{
    public interface IPackageUploadService
    {
        /// <summary>
        /// Validate the provided package archive reader before
        /// <see cref="GeneratePackageAsync(string, PackageArchiveReader, PackageStreamMetadata, User, User)"/> is
        /// called. This is useful for finding errors or warnings that should be caught before the user verifies their
        /// UI package upload.
        /// </summary>
        /// <param name="nuGetPackage">The package archive reader.</param>
        /// <param name="packageMetadata">The package metadata.</param>
        /// <param name="currentUser">The user who pushed the package.</param>
        /// <returns>The package validation result.</returns>
        Task<PackageValidationResult> ValidateBeforeGeneratePackageAsync(
            PackageArchiveReader nuGetPackage,
            PackageMetadata packageMetadata,
            User currentUser);

        Task<Package> GeneratePackageAsync(
            string id,
            PackageArchiveReader nugetPackage,
            PackageStreamMetadata packageStreamMetadata,
            User owner,
            User currentUser);

        /// <summary>
        /// Validate the provided package once the new packages owner is known. The validations performed by this
        /// method should make sense for both the UI upload and command line push. This should be called after
        /// <see cref="GeneratePackageAsync(string, PackageArchiveReader, PackageStreamMetadata, User, User)"/> but
        /// before <see cref="CommitPackageAsync(Package, Stream)"/>.
        /// </summary>
        /// <param name="package">The package entity not yet committed to the database.</param>
        /// <param name="nuGetPackage">The package archive reader.</param>
        /// <param name="owner">The owner of the package.</param>
        /// <param name="currentUser">The current user.</param>
        /// <param name="isNewPackageRegistration">Determine whether the uploaded package is a new package without existing package registration info.</param>
        /// <returns>The package validation result.</returns>
        Task<PackageValidationResult> ValidateAfterGeneratePackageAsync(
            Package package,
            PackageArchiveReader nuGetPackage,
            User owner,
            User currentUser,
            bool isNewPackageRegistration);

        /// <summary>
        /// Commit the provided package metadata and stream to the package file storage and to the database. This
        /// method commits the shared <see cref="IEntitiesContext"/>. This method can throw exceptions in exceptional
        /// cases (such as database failures). This method does not dispose the provided stream but does read it.
        /// </summary>
        /// <param name="package">The package metadata. This is assumed to already be added to the context.</param>
        /// <param name="packageFile">The seekable stream containing the package content (.nupkg).</param>
        Task<PackageCommitResult> CommitPackageAsync(Package package, Stream packageFile);
    }
}