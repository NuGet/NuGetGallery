// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    public class FlatContainerContentFileMetadataService : IContentFileMetadataService
    {
        public string PackageContentFolderName => CoreConstants.Folders.FlatContainerFolderName;
        public string PackageContentPathTemplate => CoreConstants.PackageContentFileSavePathTemplate;
    }
}