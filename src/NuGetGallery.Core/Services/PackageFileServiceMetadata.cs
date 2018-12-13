// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    public class PackageFileMetadataService : IFileMetadataService
    {
        public string FileFolderName => CoreConstants.Folders.PackagesFolderName;

        public string FileSavePathTemplate => CoreConstants.PackageFileSavePathTemplate;

        public string FileExtension => CoreConstants.NuGetPackageFileExtension;

        public string ValidationFolderName => CoreConstants.Folders.ValidationFolderName;

        public string FileBackupsFolderName => CoreConstants.Folders.PackageBackupsFolderName;

        public string FileBackupSavePathTemplate => CoreConstants.PackageFileBackupSavePathTemplate;
    }
}
