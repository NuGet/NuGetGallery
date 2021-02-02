// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading.Tasks;
using System.Web.Mvc;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public interface IPackageFileService : ICorePackageFileService
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
        /// Deletes the package readme.md file from storage.
        /// </summary>
        /// <param name="package">The package associated with the readme.</param>
        Task DeleteReadMeMdFileAsync(Package package);

        /// <summary>
        /// Saves the (pending) package readme.md file to storage.
        /// </summary>
        /// <param name="package">The package associated with the readme.</param>
        /// <param name="readMeMd">Markdown content.</param>
        Task SaveReadMeMdFileAsync(Package package, string readMeMd);

        /// <summary>
        /// Downloads the readme.md from storage.
        /// </summary>
        /// <param name="package">The package associated with the readme.</param>
        Task<string> DownloadReadMeMdFileAsync(Package package);
    }
}