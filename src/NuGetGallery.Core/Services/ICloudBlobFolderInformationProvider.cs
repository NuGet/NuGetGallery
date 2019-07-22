// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    /// <summary>
    /// Abstracts the specific storage container information allowing to reuse
    /// <see cref="CloudBlobCoreFileStorageService"/> class for any storage account.
    /// </summary>
    public interface ICloudBlobFolderInformationProvider
    {
        /// <summary>
        /// Determines if specified folder is publicly accessible.
        /// Used for creation of missing storage containers.
        /// </summary>
        /// <param name="folderName">Folder name to check.</param>
        /// <returns>True if folder is publicly accessible, false otherwise.</returns>
        bool IsPublicFolder(string folderName);

        /// <summary>
        /// Determines the content type for files stored in the specified folder.
        /// </summary>
        /// <param name="folderName">Folder name.</param>
        /// <returns>Content type string to be set for the blob.</returns>
        string GetContentType(string folderName);

        /// <summary>
        /// Determines the cache control setting for the blobs stored in the specified folder.
        /// </summary>
        /// <param name="folderName">Folder name.</param>
        /// <returns>Cache control string to be set for the blob being created.</returns>
        string GetCacheControl(string folderName);
    }
}
