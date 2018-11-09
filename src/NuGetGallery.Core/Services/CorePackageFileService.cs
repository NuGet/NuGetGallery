﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Web;
using NuGet.Services.Entities;
using NuGet.Versioning;

namespace NuGetGallery
{
    public class CorePackageFileService : ICorePackageFileService
    {
        private const string LicenseFileName = "license";

        private readonly ICoreFileStorageService _fileStorageService;
        private readonly IFileMetadataService _metadata;

        public CorePackageFileService(ICoreFileStorageService fileStorageService, IFileMetadataService metadata)
        {
            _fileStorageService = fileStorageService ?? throw new ArgumentNullException(nameof(fileStorageService));
            _metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        }

        public Task SavePackageFileAsync(Package package, Stream packageFile)
        {
            return SavePackageFileAsync(package, packageFile, overwrite: false);
        }

        public Task SavePackageFileAsync(Package package, Stream packageFile, bool overwrite)
        {
            if (packageFile == null)
            {
                throw new ArgumentNullException(nameof(packageFile));
            }

            var fileName = BuildFileName(package, _metadata.FileSavePathTemplate, _metadata.FileExtension);
            return _fileStorageService.SaveFileAsync(_metadata.FileFolderName, fileName, packageFile, overwrite);
        }

        public Task<Stream> DownloadPackageFileAsync(Package package)
        {
            var fileName = BuildFileName(package, _metadata.FileSavePathTemplate, _metadata.FileExtension);
            return _fileStorageService.GetFileAsync(_metadata.FileFolderName, fileName);
        }

        public Task<Uri> GetPackageReadUriAsync(Package package)
        {
            var fileName = BuildFileName(package, _metadata.FileSavePathTemplate, _metadata.FileExtension);
            return _fileStorageService.GetFileReadUriAsync(_metadata.FileFolderName, fileName, endOfAccess: null);
        }

        public Task<bool> DoesPackageFileExistAsync(Package package)
        {
            var fileName = BuildFileName(package, _metadata.FileSavePathTemplate, _metadata.FileExtension);
            return _fileStorageService.FileExistsAsync(_metadata.FileFolderName, fileName);
        }

        public Task SaveValidationPackageFileAsync(Package package, Stream packageFile)
        {
            if (packageFile == null)
            {
                throw new ArgumentNullException(nameof(packageFile));
            }

            var fileName = BuildFileName(
                package,
                _metadata.FileSavePathTemplate,
                _metadata.FileExtension);

            return _fileStorageService.SaveFileAsync(
                _metadata.ValidationFolderName,
                fileName,
                packageFile,
                overwrite: false);
        }

        public Task<Stream> DownloadValidationPackageFileAsync(Package package)
        {
            var fileName = BuildFileName(
                package,
                _metadata.FileSavePathTemplate,
                _metadata.FileExtension);

            return _fileStorageService.GetFileAsync(_metadata.ValidationFolderName, fileName);
        }

        public Task DeleteValidationPackageFileAsync(string id, string version)
        {
            if (version == null)
            {
                throw new ArgumentNullException(nameof(version));
            }

            var normalizedVersion = NuGetVersionFormatter.Normalize(version);
            var fileName = BuildFileName(
                id,
                normalizedVersion,
                _metadata.FileSavePathTemplate,
                _metadata.FileExtension);
            
            return _fileStorageService.DeleteFileAsync(_metadata.ValidationFolderName, fileName);
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

            var normalizedVersion = NuGetVersionFormatter.Normalize(version);

            var fileName = BuildFileName(id, normalizedVersion, _metadata.FileSavePathTemplate, _metadata.FileExtension);
            return _fileStorageService.DeleteFileAsync(_metadata.FileFolderName, fileName);
        }

        public Task<Uri> GetValidationPackageReadUriAsync(Package package, DateTimeOffset endOfAccess)
        {
            package = package ?? throw new ArgumentNullException(nameof(package));

            var fileName = BuildFileName(
                package,
                _metadata.FileSavePathTemplate,
                _metadata.FileExtension);

            return _fileStorageService.GetFileReadUriAsync(_metadata.ValidationFolderName, fileName, endOfAccess);
        }

        public Task<bool> DoesValidationPackageFileExistAsync(Package package)
        {
            var fileName = BuildFileName(package, _metadata.FileSavePathTemplate, _metadata.FileExtension);
            return _fileStorageService.FileExistsAsync(_metadata.ValidationFolderName, fileName);
        }

        public async Task StorePackageFileInBackupLocationAsync(Package package, Stream packageFile)
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
                throw new ArgumentException(CoreStrings.PackageIsMissingRequiredData, nameof(package));
            }

            string version;
            if (string.IsNullOrEmpty(package.NormalizedVersion))
            {
                version = NuGetVersion.Parse(package.Version).ToNormalizedString();
            }
            else
            {
                version = package.NormalizedVersion;
            }

            // Hash the provided stream instead of using the hash on the package. This is to avoid a backup with the
            // incorrect file name if the hash in the DB does not match the package (a potentially transient issue).
            var hash = CryptographyService.GenerateHash(
                packageFile,
                hashAlgorithmId: CoreConstants.Sha512HashAlgorithmId);
            packageFile.Position = 0;

            var fileName = BuildBackupFileName(
                package.PackageRegistration.Id,
                version,
                hash,
                _metadata.FileExtension,
                _metadata.FileBackupSavePathTemplate);

            // If the package already exists, don't even bother uploading it. The file name is based off of the hash so
            // we know the upload isn't necessary.
            if (await _fileStorageService.FileExistsAsync(_metadata.FileBackupsFolderName, fileName))
            {
                return;
            }

            try
            {
                await _fileStorageService.SaveFileAsync(_metadata.FileBackupsFolderName, fileName, packageFile);
            }
            catch (FileAlreadyExistsException)
            {
                // If the package file already exists, swallow the exception since we know the content is the same.
                return;
            }
        }

        public Task SaveLicenseFileAsync(Package package, Stream licenseFile)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            if (licenseFile == null)
            {
                throw new ArgumentNullException(nameof(licenseFile));
            }

            if (package.EmbeddedLicenseType == EmbeddedLicenseFileType.Absent)
            {
                throw new ArgumentException("Package must have an embedded license", nameof(package));
            }

            var fileName = BuildLicenseFileName(package);

            // Gallery will generally ignore the content type on license files and will use the value from the DB,
            // but we'll be nice and try to specify correct content type for them.
            var contentType = package.EmbeddedLicenseType == EmbeddedLicenseFileType.Markdown
                ? CoreConstants.MarkdownContentType
                : CoreConstants.TextContentType;

            return _fileStorageService.SaveFileAsync(_metadata.PackageContentFolderName, fileName, contentType, licenseFile, overwrite: true);
        }

        public Task<Stream> DownloadLicenseFileAsync(Package package)
        {
            var fileName = BuildLicenseFileName(package);
            return _fileStorageService.GetFileAsync(_metadata.PackageContentFolderName, fileName);
        }

        public Task DeleteLicenseFileAsync(string id, string version)
        {
            if (id == null)
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException($"{nameof(id)} cannot be empty", nameof(id));
            }

            if (version == null)
            {
                throw new ArgumentNullException(nameof(version));
            }

            if (string.IsNullOrWhiteSpace(version))
            {
                throw new ArgumentException($"{nameof(version)} cannot be empty", nameof(version));
            }

            var normalizedVersion = NuGetVersionFormatter.Normalize(version);
            var fileName = BuildLicenseFileName(id, normalizedVersion);

            return _fileStorageService.DeleteFileAsync(_metadata.PackageContentFolderName, fileName);
        }

        private string LicensePathTemplate => $"{_metadata.PackageContentPathTemplate}/{LicenseFileName}";

        private string BuildLicenseFileName(Package package)
            => BuildFileName(package, LicensePathTemplate, string.Empty);

        private string BuildLicenseFileName(string id, string version)
            => BuildFileName(id, version, LicensePathTemplate, string.Empty);

        private static string BuildBackupFileName(string id, string version, string hash, string extension, string fileBackupSavePathTemplate)
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
                fileBackupSavePathTemplate,
                id.ToLowerInvariant(),
                version.ToLowerInvariant(),
                HttpServerUtility.UrlTokenEncode(hashBytes),
                extension);
        }

        protected static string BuildFileName(Package package, string format, string extension)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            if (package.PackageRegistration == null ||
                String.IsNullOrWhiteSpace(package.PackageRegistration.Id) ||
                (String.IsNullOrWhiteSpace(package.NormalizedVersion) && String.IsNullOrWhiteSpace(package.Version)))
            {
                throw new ArgumentException(CoreStrings.PackageIsMissingRequiredData, nameof(package));
            }

            return BuildFileName(
                package.PackageRegistration.Id,
                string.IsNullOrEmpty(package.NormalizedVersion) ?
                    NuGetVersionFormatter.Normalize(package.Version) :
                    package.NormalizedVersion, format, extension);
        }

        protected static string BuildFileName(string id, string version, string pathTemplate, string extension)
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
    }
}