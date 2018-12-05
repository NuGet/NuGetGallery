// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    public class GalleryContentFileMetadataService : IContentFileMetadataService
    {
        public string PackageContentFolderName => CoreConstants.Folders.PackagesContentFolderName;
        public string PackageContentPathTemplate => CoreConstants.PackageContentFileSavePathTemplate;
    }
}