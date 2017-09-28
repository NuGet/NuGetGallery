// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public interface ICorePackageFileService
    {
        /// <summary>
        /// Saves the contents of the package to the public container for available packages.
        /// </summary>
        Task SavePackageFileAsync(Package package, Stream packageFile);

        /// <summary>
        /// Downloads the package from the file storage and reads it into a stream.
        /// </summary>
        Task<Stream> DownloadPackageFileAsync(Package package);

        /// <summary>
        /// Saves the contents of the package to the private container for packages that are being validated. If the
        /// file already exists, an exception will be thrown.
        /// </summary>
        /// <param name="package">The package metadata.</param>
        /// <param name="packageFile">The stream containing the package itself (the .nupkg).</param>
        Task SaveValidationPackageFileAsync(Package package, Stream packageFile);

        /// <summary>
        /// Downloads the validating package from the file storage and reads it into a stream. If the file does not
        /// exist, an exception will be thrown.
        /// </summary>
        /// <param name="package">The package metadata.</param>
        Task<Stream> DownloadValidationPackageFileAsync(Package package);

        /// <summary>
        /// Deletes the validating package from the file storage. If the file does not exist this method will not throw
        /// any exception.
        /// </summary>
        /// <param name="id">The package ID. This value is case-insensitive.</param>
        /// <param name="version">The package version. This value is case-insensitive and need not be normalized.</param>
        Task DeleteValidationPackageFileAsync(string id, string version);
    }
}