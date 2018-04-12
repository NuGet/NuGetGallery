// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public interface ICorePackageFileService<T> where T : IEntity
    {
        /// <summary>
        /// Saves the contents of the package to the public container for available packages.
        /// </summary>
        Task SavePackageFileAsync(T package, Stream packageFile);

        /// <summary>
        /// Downloads the package from the file storage and reads it into a stream.
        /// </summary>
        Task<Stream> DownloadPackageFileAsync(T package);

        /// <summary>
        /// Generates the URL for the specified package in the public container for available packages.
        /// </summary>
        /// <param name="package">The package metadata.</param>
        /// <returns>Package download URL</returns>
        /// <remarks>
        /// The returned URL is only intended to be used by the internal tooling and not for the user:
        /// it might not make any sense to external users as it can be, for example, a file:/// URL.
        /// </remarks>
        Task<Uri> GetPackageReadUriAsync(T package);

        /// <summary>
        /// Checks whether package file exists in the public container for available packages
        /// </summary>
        /// <param name="">The package metadata</param>
        /// <returns>True if file exists, false otherwise</returns>
        Task<bool> DoesPackageFileExistAsync(T package);

        /// <summary>
        /// Saves the contents of the package to the private container for packages that are being validated. If the
        /// file already exists, an exception will be thrown.
        /// </summary>
        /// <param name="package">The package metadata.</param>
        /// <param name="packageFile">The stream containing the package itself (the .nupkg).</param>
        Task SaveValidationPackageFileAsync(T package, Stream packageFile);

        /// <summary>
        /// Downloads the validating package from the file storage and reads it into a stream. If the file does not
        /// exist, an exception will be thrown.
        /// </summary>
        /// <param name="package">The package metadata.</param>
        Task<Stream> DownloadValidationPackageFileAsync(T package);

        /// <summary>
        /// Generates the URI for the specified validating package, which can be used to download it.
        /// </summary>
        /// <param name="package">The package metadata.</param>
        /// <param name="endOfAccess">The timestamp that limits the URI usage period.</param>
        /// <returns>Time limited (if implementation supports) URI for the validation package</returns>
        Task<Uri> GetValidationPackageReadUriAsync(T package, DateTimeOffset endOfAccess);

        /// <summary>
        /// Checks whether package file exists in the private validation container
        /// </summary>
        /// <param name="">The package metadata</param>
        /// <returns>True if file exists, false otherwise</returns>
        /// <returns>True if file exists, false otherwise</returns>
        Task<bool> DoesValidationPackageFileExistAsync(T package);

        /// <summary>
        /// Deletes the validating package from the file storage. If the file does not exist this method will not throw
        /// any exception.
        /// </summary>
        /// <param name="id">The package ID. This value is case-insensitive.</param>
        /// <param name="version">The package version. This value is case-insensitive and need not be normalized.</param>
        Task DeleteValidationPackageFileAsync(string id, string version);

        /// <summary>
        /// Deletes the nupkg from the publicly available package storage.
        /// </summary>
        /// <param name="id">The package ID. This value is case-insensitive.</param>
        /// <param name="version">The package version. This value is case-insensitive and need not be normalized.</param>
        Task DeletePackageFileAsync(string id, string version);

        /// <summary>
        /// Copies the contents of the package represented by the stream into the file storage backup location.
        /// </summary>
        Task StorePackageFileInBackupLocationAsync(T package, Stream packageFile);
    }
}