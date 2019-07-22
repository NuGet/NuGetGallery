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
        bool IsPublicFolder(string folderName);
    }
}
