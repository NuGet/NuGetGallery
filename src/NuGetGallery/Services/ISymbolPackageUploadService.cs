// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Threading.Tasks;
using NuGet.Services.Entities;

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
        /// <param name="symbolPackageStream">The Stream object for the uploaded snupkg.</param>
        /// <returns>Awaitable task with <see cref="PackageCommitResult"/></returns>
        Task<PackageCommitResult> CreateAndUploadSymbolsPackage(Package package, Stream symbolPackageStream);

        /// <summary>
        /// Validate the uploaded symbols package. This method will perform all required validations for symbols, including
        /// synchronous package validations for fail fast scenario. This method will not perform the ownership validations
        /// for the symbols package. It is the responsibility of the caller to perform ownership validations. This method
        /// does not dispose the provided stream but does read it.
        /// </summary>
        /// <param name="uploadStream">The <see cref="Stream"/> object for the uploaded snupkg.</param>
        /// <param name="currentUser">The user performing the uploads.</param>
        /// <returns>Awaitable task with <see cref="SymbolPackageValidationResult"/></returns>
        Task<SymbolPackageValidationResult> ValidateUploadedSymbolsPackage(Stream uploadStream, User currentUser);

        /// <summary>
        /// Mark the specifed symbol package for deletion and delete the corressponding snupkg as well.
        /// </summary>
        /// <param name="symbolPackage">The <see cref="SymbolPackage"/> entity to be marked for deletion</param>
        Task DeleteSymbolsPackageAsync(SymbolPackage symbolPackage);
    }
}