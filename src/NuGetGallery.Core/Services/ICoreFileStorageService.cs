// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public interface ICoreFileStorageService
    {
        Task DeleteFileAsync(string folderName, string fileName);

        Task<bool> FileExistsAsync(string folderName, string fileName);

        Task<Stream> GetFileAsync(string folderName, string fileName);

        /// <summary>
        /// Gets a reference to a file in the storage service, which can be used to open the full file data.
        /// </summary>
        /// <param name="folderName">The folder containing the file to open</param>
        /// <param name="fileName">The file within that folder to open</param>
        /// <param name="ifNoneMatch">The <see cref="IFileReference.ContentId"/> value to use in an If-None-Match request</param>
        /// <returns>A <see cref="IFileReference"/> representing the file reference</returns>
        Task<IFileReference> GetFileReferenceAsync(string folderName, string fileName, string ifNoneMatch = null);

        /// <summary>
        /// Generates the storage file URI (which is optionally time limited)
        /// </summary>
        /// <param name="folderName">The folder containing the file.</param>
        /// <param name="fileName">The file within the <paramref name="folderName"/>.</param>
        /// <param name="endOfAccess">Optional end of access timestamp.</param>
        /// <returns>Time limited URI (if requested and implementation supports it) for the specified file.</returns>
        Task<Uri> GetFileReadUriAsync(string folderName, string fileName, DateTimeOffset? endOfAccess);

        Task SaveFileAsync(string folderName, string fileName, Stream packageFile, bool overwrite = true);
    }
}
