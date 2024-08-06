// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    public interface IFileMetadataService
    {
        /// <summary>
        /// This is the public folder name where files will be copied. For example <see cref="CoreConstants.PackagesFolderName"/>
        /// </summary>
        string FileFolderName { get; }

        /// <summary>
        /// The save file path template. For example <see cref="CoreConstants.PackageFileSavePathTemplate"/>
        /// </summary>
        string FileSavePathTemplate { get; }

        /// <summary>
        /// The file extension. For example <see cref="CoreConstants.NuGetPackageFileExtension"/>
        /// </summary>
        string FileExtension { get; }

        /// <summary>
        /// The validation folder name. For example <see cref="CoreConstants.ValidationFolderName"/>
        /// </summary>
        string ValidationFolderName { get; }

        /// <summary>
        /// The backups folder name. For example <see cref="CoreConstants.PackageBackupsFolderName"/>
        /// </summary>
        string FileBackupsFolderName { get; }

        /// <summary>
        /// The path template for the backup. For example <see cref="CoreConstants.PackageFileBackupSavePathTemplate"/>
        /// </summary>
        string FileBackupSavePathTemplate { get; }
    }
}
