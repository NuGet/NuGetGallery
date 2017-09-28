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
    }
}