// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    public class SymbolPackageFileMetadataService : IFileMetadataService
    {
        public string FileFolderName => CoreConstants.SymbolPackagesFolderName;

        public string FileSavePathTemplate => CoreConstants.PackageFileSavePathTemplate;

        public string FileExtension => CoreConstants.NuGetSymbolPackageFileExtension;

        public string ValidationFolderName => CoreConstants.ValidationFolderName;

        public string FileBackupsFolderName => CoreConstants.SymbolPackageBackupsFolderName;

        public string FileBackupSavePathTemplate => CoreConstants.PackageFileBackupSavePathTemplate;
    }
}
