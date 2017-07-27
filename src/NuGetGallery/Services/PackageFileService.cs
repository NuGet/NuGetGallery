﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using NuGet.Versioning;

namespace NuGetGallery
{
    public class PackageFileService : IPackageFileService
    {
        private readonly IFileStorageService _fileStorageService;

        public PackageFileService(IFileStorageService fileStorageService)
        {
            _fileStorageService = fileStorageService;
        }

        public Task<ActionResult> CreateDownloadPackageActionResultAsync(Uri requestUrl, Package package)
        {
            var fileName = BuildFileName(package, Constants.PackageFileSavePathTemplate, Constants.NuGetPackageFileExtension);
            return _fileStorageService.CreateDownloadFileActionResultAsync(requestUrl, Constants.PackagesFolderName, fileName);
        }

        public Task<ActionResult> CreateDownloadPackageActionResultAsync(Uri requestUrl, string id, string version)
        {
            var fileName = BuildFileName(id, version, Constants.PackageFileSavePathTemplate, Constants.NuGetPackageFileExtension);
            return _fileStorageService.CreateDownloadFileActionResultAsync(requestUrl, Constants.PackagesFolderName, fileName);
        }

        public Task DeletePackageFileAsync(string id, string version)
        {
            if (String.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (String.IsNullOrWhiteSpace(version))
            {
                throw new ArgumentNullException(nameof(version));
            }

            var fileName = BuildFileName(id, version, Constants.PackageFileSavePathTemplate, Constants.NuGetPackageFileExtension);
            return _fileStorageService.DeleteFileAsync(Constants.PackagesFolderName, fileName);
        }

        public Task SavePackageFileAsync(Package package, Stream packageFile)
        {
            if (packageFile == null)
            {
                throw new ArgumentNullException(nameof(packageFile));
            }

            var fileName = BuildFileName(package, Constants.PackageFileSavePathTemplate, Constants.NuGetPackageFileExtension);
            return _fileStorageService.SaveFileAsync(Constants.PackagesFolderName, fileName, packageFile, overwrite: false);
        }


        /// <summary>
        /// Saves a ReadMe file asynchronously to the Pending ReadMe file folder.
        /// </summary>
        /// <param name="package">The package to which this readme belongs.</param>
        /// <param name="readMe">The stream representing the readme's data.</param>
        public Task SaveReadMeFileAsync(Package package, Stream readMe, string fileExtension)
        {
            if (readMe == null)
            {
                throw new ArgumentNullException(nameof(readMe));
            }
            if (string.IsNullOrEmpty(fileExtension))
            {
                throw new ArgumentException(nameof(fileExtension));
            }
            var fileName = BuildFileName(package, Constants.ReadMeFileSavePathTemplatePending, fileExtension);
            return _fileStorageService.SaveFileAsync(Constants.ReadMeFolderName, fileName, readMe, overwrite:false);
        }

        public Task StorePackageFileInBackupLocationAsync(Package package, Stream packageFile)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }
            
            if (packageFile == null)
            {
                throw new ArgumentNullException(nameof(packageFile));
            }

            if (package.PackageRegistration == null ||
                string.IsNullOrWhiteSpace(package.PackageRegistration.Id) ||
                (string.IsNullOrWhiteSpace(package.NormalizedVersion) && string.IsNullOrWhiteSpace(package.Version)))
            {
                throw new ArgumentException(Strings.PackageIsMissingRequiredData, nameof(package));
            }

            var fileName = BuildBackupFileName(package.PackageRegistration.Id, string.IsNullOrEmpty(package.NormalizedVersion) 
                ? NuGetVersion.Parse(package.Version).ToNormalizedString() : package.NormalizedVersion, package.Hash);
            return _fileStorageService.SaveFileAsync(Constants.PackageBackupsFolderName, fileName, packageFile);
        }

        public async Task<Stream> DownloadPackageFileAsync(Package package)
        {
            var fileName = BuildFileName(package, Constants.PackageFileSavePathTemplate, Constants.NuGetPackageFileExtension);
            return (await _fileStorageService.GetFileAsync(Constants.PackagesFolderName, fileName));
        }

        private static string BuildFileName(string id, string version, string formatter, string fileExtension)
        {
            if (id == null)
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (version == null)
            {
                throw new ArgumentNullException(nameof(version));
            }

            // Note: packages should be saved and retrieved in blob storage using the lower case version of their filename because
            // a) package IDs can and did change case over time
            // b) blob storage is case sensitive
            // c) we don't want to hit the database just to look up the right case
            // and remember - version can contain letters too.
            return String.Format(
                CultureInfo.InvariantCulture,
                formatter,
                id.ToLowerInvariant(),
                version.ToLowerInvariant(),
                fileExtension);
        }

        private static string BuildFileName(Package package, string formatter, string fileExtension)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            if (package.PackageRegistration == null ||
                String.IsNullOrWhiteSpace(package.PackageRegistration.Id) ||
                (String.IsNullOrWhiteSpace(package.NormalizedVersion) && String.IsNullOrWhiteSpace(package.Version)))
            {
                throw new ArgumentException(Strings.PackageIsMissingRequiredData, nameof(package));
            }

            return BuildFileName(
                package.PackageRegistration.Id,
                String.IsNullOrEmpty(package.NormalizedVersion) ?
                    NuGetVersionFormatter.Normalize(package.Version) :
                    package.NormalizedVersion, formatter, fileExtension);
        }

        private static string BuildBackupFileName(string id, string version, string hash)
        {
            if (id == null)
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (version == null)
            {
                throw new ArgumentNullException(nameof(version));
            }

            if (hash == null)
            {
                throw new ArgumentNullException(nameof(hash));
            }

            var hashBytes = Convert.FromBase64String(hash);

            return string.Format(
                CultureInfo.InvariantCulture,
                Constants.PackageFileBackupSavePathTemplate,
                id,
                version,
                HttpServerUtility.UrlTokenEncode(hashBytes),
                Constants.NuGetPackageFileExtension);
        }
    }
}