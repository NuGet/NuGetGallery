// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Threading.Tasks;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    /// <summary>
    /// Provides license file related operations
    /// </summary>
    public interface ICoreLicenseFileService
    {
        /// <summary>
        /// Saves the license file to the public container for package content.
        /// </summary>
        Task SaveLicenseFileAsync(Package package, Stream licenseFile);

        /// <summary>
        /// Saves the license file from package stream.
        /// </summary>
        /// <param name="package">Package information.</param>
        /// <param name="packageStream">Package stream with .nupkg contents.</param>
        Task ExtractAndSaveLicenseFileAsync(Package package, Stream packageStream);

        /// <summary>
        /// Downloads previously saved license file for a specified package.
        /// </summary>
        Task<Stream> DownloadLicenseFileAsync(Package package);

        /// <summary>
        /// Deletes the license file for the package from the publicly available storage for the package content.
        /// </summary>
        /// <param name="id">The package ID. This value is case-insensitive.</param>
        /// <param name="version">The package version. This value is case-insensitive and need not be normalized.</param>
        /// <returns></returns>
        Task DeleteLicenseFileAsync(string id, string version);
    }
}
