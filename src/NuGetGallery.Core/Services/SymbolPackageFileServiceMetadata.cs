// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery
{
    public class SymbolPackageFileMetadataService : IFileMetadataService
    {
        public string FileFolderName => CoreConstants.Folders.SymbolPackagesFolderName;

        public string FileSavePathTemplate => CoreConstants.PackageFileSavePathTemplate;

        public string FileExtension => CoreConstants.NuGetSymbolPackageFileExtension;

        public string ValidationFolderName => CoreConstants.Folders.ValidationFolderName;

        public string FileBackupsFolderName => CoreConstants.Folders.SymbolPackageBackupsFolderName;

        public string FileBackupSavePathTemplate => CoreConstants.PackageFileBackupSavePathTemplate;
    }
}
