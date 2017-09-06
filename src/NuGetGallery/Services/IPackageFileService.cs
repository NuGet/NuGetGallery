// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.IO;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace NuGetGallery
{
    public interface IPackageFileService
    {
        /// <summary>
        /// Creates an ActionResult that allows a third-party client to download the nupkg for the package.
        /// </summary>
        Task<ActionResult> CreateDownloadPackageActionResultAsync(Uri requestUrl, Package package);

        /// <summary>
        /// Creates an ActionResult that allows a third-party client to download the nupkg for the package.
        /// </summary>
        Task<ActionResult> CreateDownloadPackageActionResultAsync(Uri requestUrl, string unsafeId, string unsafeVersion);

        /// <summary>
        /// Deletes the nupkg from the file storage.
        /// </summary>
        Task DeletePackageFileAsync(string id, string version);

        /// <summary>
        /// Saves the contents of the package represented by the stream into the file storage.
        /// </summary>
        Task SavePackageFileAsync(Package package, Stream packageFile);

        /// <summary>
        /// Deletes the package readme.md file from storage.
        /// </summary>
        Task DeleteReadMeFileAsync(string id, string version, bool isPending = false);

        /// <summary>
        /// Saves the package readme.md file to storage.
        /// </summary>
        /// <param name="package">The package that this file belongs to</param>
        /// <param name="readMe">The stream representing the readme.md file</param>
        Task SaveReadMeFileAsync(Package package, Stream readMe);

        /// <summary>
        /// Copies the contents of the package represented by the stream into the file storage backup location.
        /// </summary>
        Task StorePackageFileInBackupLocationAsync(Package package, Stream packageFile);

        /// <summary>
        /// Downloads the package from the file storage and reads it into a Stream asynchronously.
        /// </summary>
        Task<Stream> DownloadPackageFileAsync(Package packge);

        /// <summary>
        /// Downloads the readme.md from the file storage and reads it into a Stream asynchronously.
        /// </summary>
        Task<Stream> DownloadReadMeFileAsync(Package package, bool isPending = false);
    }
}