// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Threading.Tasks;
using NuGet.Packaging;
using NuGetGallery.Packaging;

namespace NuGetGallery
{
    /// <summary>
    /// Non-exceptional results of calling <see cref="IPackageUploadService.CommitPackageAsync(Package, Stream)"/>.
    /// </summary>
    public enum PackageCommitResult
    {
        /// <summary>
        /// The package was successfully committed to the package file storage and to the database.
        /// </summary>
        Success,

        /// <summary>
        /// The package file conflicts with an existing package file. The package was not committed to the database.
        /// </summary>
        Conflict,
    }

    public interface IPackageUploadService
    {
        Task<Package> GeneratePackageAsync(
            string id,
            PackageArchiveReader nugetPackage,
            PackageStreamMetadata packageStreamMetadata,
            User owner,
            User currentUser);

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