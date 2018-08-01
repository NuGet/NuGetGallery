// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Threading.Tasks;
using NuGetGallery.Packaging;

namespace NuGetGallery
{
    /// <summary>
    /// Business logic for uploading symbol to containers and creating db entities.
    /// This will be the common code base for validating any symbols related things between
    /// API and Packages controllers.
    /// </summary>
    public interface ISymbolPackageUploadService
    {
        /// <summary>
        /// Create the symbols package entry in database, and upload the package to validation/uploads container for symbols.
        /// This method commits the shared <see cref="IEntitiesContext"/>. This method can throw exceptions in exceptional
        /// cases (such as database failures). This method does not dispose the provided stream but does read it.
        /// </summary>
        /// <param name="package">The package for which the symbols are to be uploaded.</param>
        /// <param name="packageStreamMetadata">The metadata object for the uploaded snupkg.</param>
        /// <param name="symbolPackageFile">The seekable stream containing the symbol package content (.snupkg).</param>
        /// <returns>Awaitable task with <see cref="PackageCommitResult"/></returns>
        Task<PackageCommitResult> CreateAndUploadSymbolsPackage(Package package, PackageStreamMetadata packageStreamMetadata, Stream symbolPackageFile);
    }
}