// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Threading.Tasks;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    /// <summary>
    /// Provides readmes file related operations
    /// </summary>
    public interface ICoreReadmeFileService
    {

        /// <summary>
        /// Saves the package readme.md file to storage. This method should throw if the package
        /// does not have an embedded readme file 
        /// </summary>
        /// <param name="package">The package associated with the readme.</param>
        /// <param name="readmeFile">The content of readme file.</param>
        Task SaveReadmeFileAsync(Package package, Stream readmeFile);

        /// <summary>
        /// Save the readme file from package stream. This method should throw if the package
        /// does not have an embedded readme file 
        /// </summary>
        /// <param name="package">Package information.</param>
        /// <param name="packageStream">Package stream with .nupkg contents.</param>
        Task ExtractAndSaveReadmeFileAsync(Package package, Stream packageStream);

        /// <summary>
        /// Downloads previously saved readme file for a specified package.
        /// </summary>
        Task<string> DownloadReadmeFileAsync(Package package);

        /// <summary>
        /// Deletes the readme file for the package from the publicly available storage for the package content.
        /// </summary>
        /// <param name="id">The package ID. This value is case-insensitive.</param>
        /// <param name="version">The package version. This value is case-insensitive and need not be normalized.</param>
        /// <returns></returns>
        Task DeleteReadmeFileAsync(string id, string version);
    }
}