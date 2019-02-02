// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;

namespace NuGetGallery
{
    public class GalleryCloudBlobFolderDescription : ICloudBlobFolderDescription
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

        public string GetCacheControl(string folderName)
        {
            switch (folderName)
            {
                case CoreConstants.Folders.PackagesFolderName:
                case CoreConstants.Folders.SymbolPackagesFolderName:
                case CoreConstants.Folders.ValidationFolderName:
                    return CoreConstants.DefaultCacheControl;

                case CoreConstants.Folders.PackageBackupsFolderName:
                case CoreConstants.Folders.UploadsFolderName:
                case CoreConstants.Folders.SymbolPackageBackupsFolderName:
                case CoreConstants.Folders.DownloadsFolderName:
                case CoreConstants.Folders.PackageReadMesFolderName:
                case CoreConstants.Folders.ContentFolderName:
                case CoreConstants.Folders.RevalidationFolderName:
                case CoreConstants.Folders.StatusFolderName:
                case CoreConstants.Folders.UserCertificatesFolderName:
                case CoreConstants.Folders.PackagesContentFolderName:
                case CoreConstants.Folders.FlatContainerFolderName:
                    return null;

                default:
                    throw new InvalidOperationException(
                        string.Format(CultureInfo.CurrentCulture, "The folder name {0} is not supported.", folderName));
            }
        }

        public string GetContentType(string folderName)
        {
            switch (folderName)
            {
                case CoreConstants.Folders.PackagesFolderName:
                case CoreConstants.Folders.PackageBackupsFolderName:
                case CoreConstants.Folders.UploadsFolderName:
                case CoreConstants.Folders.ValidationFolderName:
                case CoreConstants.Folders.SymbolPackagesFolderName:
                case CoreConstants.Folders.SymbolPackageBackupsFolderName:
                case CoreConstants.Folders.FlatContainerFolderName:
                    return CoreConstants.PackageContentType;

                case CoreConstants.Folders.DownloadsFolderName:
                    return CoreConstants.OctetStreamContentType;

                case CoreConstants.Folders.PackageReadMesFolderName:
                    return CoreConstants.TextContentType;

                case CoreConstants.Folders.ContentFolderName:
                case CoreConstants.Folders.RevalidationFolderName:
                case CoreConstants.Folders.StatusFolderName:
                    return CoreConstants.JsonContentType;

                case CoreConstants.Folders.UserCertificatesFolderName:
                    return CoreConstants.CertificateContentType;

                case CoreConstants.Folders.PackagesContentFolderName:
                    return CoreConstants.OctetStreamContentType;

                default:
                    throw new InvalidOperationException(
                        string.Format(CultureInfo.CurrentCulture, "The folder name {0} is not supported.", folderName));
            }
        }

        public bool IsPublicContainer(string folderName)
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