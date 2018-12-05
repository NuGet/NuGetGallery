// Copyright (c) .NET Foundation. All rights reserved.
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

            var fileName = FileNameHelper.BuildFileName(package, _metadata.FileSavePathTemplate, _metadata.FileExtension);
            return _fileStorageService.SaveFileAsync(_metadata.FileFolderName, fileName, packageFile, overwrite);
        }

        public Task<Stream> DownloadPackageFileAsync(Package package)
        {
            var fileName = FileNameHelper.BuildFileName(package, _metadata.FileSavePathTemplate, _metadata.FileExtension);
            return _fileStorageService.GetFileAsync(_metadata.FileFolderName, fileName);
        }

        public Task<Uri> GetPackageReadUriAsync(Package package)
        {
            var fileName = FileNameHelper.BuildFileName(package, _metadata.FileSavePathTemplate, _metadata.FileExtension);
            return _fileStorageService.GetFileReadUriAsync(_metadata.FileFolderName, fileName, endOfAccess: null);
        }

        public Task<bool> DoesPackageFileExistAsync(Package package)
        {
            var fileName = FileNameHelper.BuildFileName(package, _metadata.FileSavePathTemplate, _metadata.FileExtension);
            return _fileStorageService.FileExistsAsync(_metadata.FileFolderName, fileName);
        }

        public Task SaveValidationPackageFileAsync(Package package, Stream packageFile)
        {
            if (packageFile == null)
            {
                throw new ArgumentNullException(nameof(packageFile));
            }

            var fileName = FileNameHelper.BuildFileName(
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
            var fileName = FileNameHelper.BuildFileName(
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
            var fileName = FileNameHelper.BuildFileName(
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

            var fileName = FileNameHelper.BuildFileName(id, normalizedVersion, _metadata.FileSavePathTemplate, _metadata.FileExtension);
            return _fileStorageService.DeleteFileAsync(_metadata.FileFolderName, fileName);
        }

        public Task<Uri> GetValidationPackageReadUriAsync(Package package, DateTimeOffset endOfAccess)
        {
            package = package ?? throw new ArgumentNullException(nameof(package));

            var fileName = FileNameHelper.BuildFileName(
                package,
                _metadata.FileSavePathTemplate,
                _metadata.FileExtension);

            return _fileStorageService.GetFileReadUriAsync(_metadata.ValidationFolderName, fileName, endOfAccess);
        }

        public Task<bool> DoesValidationPackageFileExistAsync(Package package)
        {
            var fileName = FileNameHelper.BuildFileName(package, _metadata.FileSavePathTemplate, _metadata.FileExtension);
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
    }
}