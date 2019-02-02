// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    public interface ICloudBlobFolderDescription
    {
        /// <summary>
        /// Returns the content type to use when saving blobs to the specified folder.
        /// </summary>
        string GetContentType(string folderName);

        /// <summary>
        /// Returns the cache control value to set on the blob saved to the specified folder.
        /// </summary>
        /// <remarks>May return null, meaning 'no cache control value should be set'.</remarks>
        string GetCacheControl(string folderName);

        /// <summary>
        /// Returns true if the container corresponding to the specified folder is supposed to be public.
        /// </summary>
        /// <remarks>
        /// Used when generating public links to blobs in that container (blobs in public container does not
        /// need SAS tokens) and when it attempts to create the container.
        /// </remarks>
        bool IsPublicContainer(string folderName);
    }
}
