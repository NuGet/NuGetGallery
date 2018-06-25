// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGetGallery;

namespace NuGet.Services.Validation.Orchestrator
{
    /// <summary>
    /// All the metadata needed for the files manipulation in the validation pipeline - save, delete, copy.
    /// </summary>
    public class PackageValidationFileServiceMetadata : IValidationFileServiceMetadata
    {
        public string FilePublicFolderName => CoreConstants.PackagesFolderName;

        public string FileSavePathTemplate => CoreConstants.PackageFileSavePathTemplate;

        public string FileExtension => CoreConstants.NuGetPackageFileExtension;

        public string ValidationFolderName => CoreConstants.ValidationFolderName;

        public string FileBackupsFolderName => CoreConstants.PackageBackupsFolderName;

        public string FileBackupSavePathTemplate => CoreConstants.PackageFileBackupSavePathTemplate;
    }
}
