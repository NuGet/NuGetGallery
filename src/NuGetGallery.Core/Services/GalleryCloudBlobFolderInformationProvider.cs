// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;

namespace NuGetGallery
{
    public class GalleryCloudBlobFolderInformationProvider : ICloudBlobFolderInformationProvider
    {
        private static readonly HashSet<string> KnownPublicFolders = new HashSet<string> {
            CoreConstants.Folders.PackagesFolderName,
            CoreConstants.Folders.PackageBackupsFolderName,
            CoreConstants.Folders.DownloadsFolderName,
            CoreConstants.Folders.SymbolPackagesFolderName,
            CoreConstants.Folders.SymbolPackageBackupsFolderName,
            CoreConstants.Folders.FlatContainerFolderName,
        };

        private static readonly HashSet<string> KnownPrivateFolders = new HashSet<string> {
            CoreConstants.Folders.ContentFolderName,
            CoreConstants.Folders.UploadsFolderName,
            CoreConstants.Folders.PackageReadMesFolderName,
            CoreConstants.Folders.ValidationFolderName,
            CoreConstants.Folders.UserCertificatesFolderName,
            CoreConstants.Folders.RevalidationFolderName,
            CoreConstants.Folders.StatusFolderName,
            CoreConstants.Folders.PackagesContentFolderName,
        };

        public bool IsPublicFolder(string folderName)
        {
            if (KnownPublicFolders.Contains(folderName))
            {
                return true;
            }

            if (KnownPrivateFolders.Contains(folderName))
            {
                return false;
            }

            throw new InvalidOperationException(
                string.Format(CultureInfo.CurrentCulture, "The folder name {0} is not supported.", folderName));
        }
    }
}
