// Copyright (c) .NET Foundation. All rights reserved.
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
            var fileName = BuildPackageFileName(package);
            return _fileStorageService.CreateDownloadFileActionResultAsync(requestUrl, Constants.PackagesFolderName, fileName);
        }

        public Task<ActionResult> CreateDownloadPackageActionResultAsync(Uri requestUrl, string id, string version)
        {
            var fileName = BuildPackageFileName(id, version);
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

            var fileName = BuildPackageFileName(id, version);
            return _fileStorageService.DeleteFileAsync(Constants.PackagesFolderName, fileName);
        }

        public Task SavePackageFileAsync(Package package, Stream packageFile)
        {
            if (packageFile == null)
            {
                throw new ArgumentNullException(nameof(packageFile));
            }

            var fileName = BuildPackageFileName(package);
            return _fileStorageService.SaveFileAsync(Constants.PackagesFolderName, fileName, packageFile, overwrite: false);
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
            var fileName = BuildPackageFileName(package);
            return (await _fileStorageService.GetFileAsync(Constants.PackagesFolderName, fileName));
        }
        
        public async Task<Stream> DownloadReadmeFileAsync(Package package, string extension)
        {
            if(package == null)
            {
                throw new ArgumentNullException("Package cannot be null!");
            }
            var fileName = BuildReadmeFileName(package, extension);
            return (await _fileStorageService.GetFileAsync(Constants.PackagesReadMeFolderName, fileName));
        }

        private static string BuildPackageFileName(string id, string version)
        {
            return BuildFileName(Constants.PackageFileSavePathTemplate, id, version, Constants.NuGetPackageFileExtension);
        }

        public static string BuildReadmeFileName(string id, string version, string extension)
        {
            return BuildFileName(Constants.ReadMeFileSavePathTemplateActive, id, version, extension);
        }

        private static string BuildReadMeMarkdownFileName(string id, string version)
        {
            return BuildFileName(Constants.ReadMeFileSavePathTemplateActive, id, version, Constants.MarkdownFileExtension);
        }

        private static string BuildFileName(string pathTemplate, string id, string version, string extension)

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
                pathTemplate,
                id.ToLowerInvariant(),
                version.ToLowerInvariant(),
                extension);
        }
        
        private static string BuildPackageFileName(Package package)
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

            return BuildPackageFileName(
                package.PackageRegistration.Id, 
                String.IsNullOrEmpty(package.NormalizedVersion) ?
                    NuGetVersionFormatter.Normalize(package.Version) :
                    package.NormalizedVersion);
        }
        
        private static string BuildReadmeFileName(Package package, string extension)
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
            
            return BuildReadmeFileName(
                package.PackageRegistration.Id,
                String.IsNullOrEmpty(package.NormalizedVersion) ?
                    NuGetVersionFormatter.Normalize(package.Version) :
                    package.NormalizedVersion, extension);
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