// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Jobs.Validation;
using NuGet.Services.Entities;
using NuGet.Services.Validation.Orchestrator.Telemetry;
using NuGetGallery;
using NuGetGallery.Packaging;

namespace NuGet.Services.Validation.Orchestrator
{
    public class ValidationFileService : CorePackageFileService, IValidationFileService
    {
        /// <summary>
        /// The value picked today is based off of the maximum duration we wait when downloading packages using the
        /// <see cref="IFileDownloader"/>.
        /// </summary>
        protected static readonly TimeSpan AccessDuration = TimeSpan.FromMinutes(10);

        protected readonly ICoreFileStorageService _fileStorageService;
        protected readonly IFileDownloader _fileDownloader;
        protected readonly ITelemetryService _telemetryService;
        protected readonly ILogger<ValidationFileService> _logger;
        protected IFileMetadataService _fileMetadataService;

        public ValidationFileService(
            ICoreFileStorageService fileStorageService,
            IFileDownloader fileDownloader,
            ITelemetryService telemetryService,
            ILogger<ValidationFileService> logger,
            IFileMetadataService fileMetadataService) : base(fileStorageService, fileMetadataService)
        {
            _fileStorageService = fileStorageService ?? throw new ArgumentNullException(nameof(fileStorageService));
            _fileDownloader = fileDownloader ?? throw new ArgumentNullException(nameof(fileDownloader));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _fileMetadataService = fileMetadataService ?? throw new ArgumentNullException(nameof(fileMetadataService));
        }

        #region Core methods to be to be invoked 
        public static string BuildFileName(PackageValidationSet validationSet, string pathTemplate, string extension)
        {
            string id = validationSet.PackageId;
            string version = validationSet.PackageNormalizedVersion;
            return FileNameHelper.BuildFileName(id, version, pathTemplate, extension);
        }

        public Task<Uri> GetPackageReadUriAsync(PackageValidationSet validationSet)
        {
            var fileName = BuildFileName(validationSet,
                _fileMetadataService.FileSavePathTemplate,
                _fileMetadataService.FileExtension);
            return _fileStorageService.GetFileReadUriAsync(_fileMetadataService.FileFolderName, fileName, endOfAccess: null);
        }

        public Task DeleteValidationPackageFileAsync(PackageValidationSet validationSet)
        {
            var fileName = BuildFileName(validationSet, _fileMetadataService.FileSavePathTemplate, _fileMetadataService.FileExtension);
            return _fileStorageService.DeleteFileAsync(_fileMetadataService.ValidationFolderName, fileName);
        }

        public Task<bool> DoesPackageFileExistAsync(PackageValidationSet validationSet)
        {
            var fileName = BuildFileName(validationSet, _fileMetadataService.FileSavePathTemplate, _fileMetadataService.FileExtension);
            return _fileStorageService.FileExistsAsync(_fileMetadataService.FileFolderName, fileName);
        }

        private async Task StorePackageFileInBackupLocationAsync(PackageValidationSet validationSet, Stream packageFile)
        {
            Package packageFromValidationSet = new Package()
            {
                PackageRegistration = new PackageRegistration() { Id = validationSet.PackageId },
                NormalizedVersion = validationSet.PackageNormalizedVersion,
                Key = validationSet.PackageKey.Value,
            };

            await StorePackageFileInBackupLocationAsync(packageFromValidationSet, packageFile);
        }

        public Task<bool> DoesValidationPackageFileExistAsync(PackageValidationSet validationSet)
        {
            var fileName = BuildFileName(validationSet, _fileMetadataService.FileSavePathTemplate, _fileMetadataService.FileExtension);
            return _fileStorageService.FileExistsAsync(_fileMetadataService.ValidationFolderName, fileName);
        }

        public Task DeletePackageFileAsync(PackageValidationSet validationSet)
        {
            var fileName = BuildFileName(validationSet, _fileMetadataService.FileSavePathTemplate, _fileMetadataService.FileExtension);
            return _fileStorageService.DeleteFileAsync(_fileMetadataService.FileFolderName, fileName);
        }
        #endregion

        public async Task<Stream> DownloadPackageFileToDiskAsync(PackageValidationSet validationSet)
        {
            var fileUri = await GetPackageReadUriAsync(validationSet);
            var result = await _fileDownloader.DownloadAsync(fileUri, CancellationToken.None);
            return result.GetStreamOrThrow();
        }

        public Task CopyValidationPackageForValidationSetAsync(PackageValidationSet validationSet)
        {
            var srcFileName = BuildFileName(validationSet,
                _fileMetadataService.FileSavePathTemplate,
                _fileMetadataService.FileExtension);

            return CopyFileAsync(
                _fileMetadataService.ValidationFolderName,
                srcFileName,
                _fileMetadataService.ValidationFolderName,
                BuildValidationSetPackageFileName(validationSet, _fileMetadataService.FileExtension),
                AccessConditionWrapper.GenerateEmptyCondition());
        }

        public async Task BackupPackageFileFromValidationSetPackageAsync(PackageValidationSet validationSet)
        {
            if (validationSet.ValidatingType == ValidatingType.Generic)
            {
                throw new ArgumentException(
                    $"This method is not supported for validation sets of validating type {validationSet.ValidatingType}",
                    nameof(validationSet));
            }

            using (_telemetryService.TrackDurationToBackupPackage(validationSet))
            {
                _logger.LogInformation(
                    "Backing up package for validation set {ValidationTrackingId} ({PackageId} {PackageVersion}).",
                    validationSet.ValidationTrackingId,
                    validationSet.PackageId,
                    validationSet.PackageNormalizedVersion);

                var packageUri = await GetPackageForValidationSetReadUriAsync(
                    validationSet,
                    DateTimeOffset.UtcNow.Add(AccessDuration));

                using (var result = await _fileDownloader.DownloadAsync(packageUri, CancellationToken.None))
                {
                    await StorePackageFileInBackupLocationAsync(validationSet, result.GetStreamOrThrow());
                }
            }
        }

        public Task<string> CopyPackageFileForValidationSetAsync(PackageValidationSet validationSet)
        {
            var srcFileName = BuildFileName(validationSet, _fileMetadataService.FileSavePathTemplate, _fileMetadataService.FileExtension);

            return CopyFileAsync(
                _fileMetadataService.FileFolderName,
                srcFileName,
                _fileMetadataService.ValidationFolderName,
                BuildValidationSetPackageFileName(validationSet, _fileMetadataService.FileExtension),
                AccessConditionWrapper.GenerateEmptyCondition());
        }

        public virtual Task CopyValidationPackageToPackageFileAsync(PackageValidationSet validationSet)
        {
            var fileName = BuildFileName(validationSet,
                _fileMetadataService.FileSavePathTemplate,
                _fileMetadataService.FileExtension);

            return CopyFileAsync(
                _fileMetadataService.ValidationFolderName,
                fileName,
                _fileMetadataService.FileFolderName,
                fileName,
                AccessConditionWrapper.GenerateIfNotExistsCondition());
        }

        public virtual Task CopyValidationSetPackageToPackageFileAsync(
            PackageValidationSet validationSet,
            IAccessCondition destAccessCondition)
        {
            var srcFileName = BuildValidationSetPackageFileName(validationSet,
                _fileMetadataService.FileExtension);

            var destFileName = BuildFileName(validationSet,
                _fileMetadataService.FileSavePathTemplate,
                _fileMetadataService.FileExtension);

            return CopyFileAsync(
                _fileMetadataService.ValidationFolderName,
                srcFileName,
                _fileMetadataService.FileFolderName,
                destFileName,
                destAccessCondition);
        }

        public Task<bool> DoesValidationSetPackageExistAsync(PackageValidationSet validationSet)
        {
            var fileName = BuildValidationSetPackageFileName(validationSet, _fileMetadataService.FileExtension);
            
            return _fileStorageService.FileExistsAsync(_fileMetadataService.ValidationFolderName, fileName);
        }

        public Task DeletePackageForValidationSetAsync(PackageValidationSet validationSet)
        {
            var fileName = BuildValidationSetPackageFileName(validationSet, _fileMetadataService.FileExtension);

            _logger.LogInformation(
                "Deleting package for validation set {ValidationTrackingId} from {FolderName}/{FileName}.",
                validationSet.ValidationTrackingId,
                _fileMetadataService.ValidationFolderName,
                fileName);

            return _fileStorageService.DeleteFileAsync(_fileMetadataService.ValidationFolderName, fileName);
        }

        public Task<Uri> GetPackageForValidationSetReadUriAsync(PackageValidationSet validationSet, DateTimeOffset endOfAccess)
        {
            var fileName = BuildValidationSetPackageFileName(validationSet, _fileMetadataService.FileExtension);

            return _fileStorageService.GetFileReadUriAsync(_fileMetadataService.ValidationFolderName, fileName, endOfAccess);
        }

        public Task CopyPackageUrlForValidationSetAsync(PackageValidationSet validationSet, string srcPackageUrl)
        {
            var destFileName = BuildValidationSetPackageFileName(validationSet, _fileMetadataService.FileExtension);

            _logger.LogInformation(
                "Copying URL {SrcPackageUrl} to {DestFolderName}/{DestFileName}.",
                srcPackageUrl,
                _fileMetadataService.ValidationFolderName,
                srcPackageUrl);

            return _fileStorageService.CopyFileAsync(
                new Uri(srcPackageUrl),
                _fileMetadataService.ValidationFolderName,
                destFileName,
                AccessConditionWrapper.GenerateEmptyCondition());
        }

        public async Task UpdatePackageBlobPropertiesAsync(PackageValidationSet validationSet)
        {
            var fileName = BuildFileName(
                validationSet,
                _fileMetadataService.FileSavePathTemplate,
                _fileMetadataService.FileExtension);

            // This will throw if the ETag changes between read and write operations.
            await _fileStorageService.SetPropertiesAsync(
                _fileMetadataService.FileFolderName,
                fileName,
                async (lazyStream, blobProperties) =>
                {
                    // Update the cache control only if the cache control is not the same as the default value.
                    if (!string.Equals(blobProperties.CacheControl, CoreConstants.DefaultCacheControl, StringComparison.OrdinalIgnoreCase))
                    {
                        blobProperties.CacheControl = CoreConstants.DefaultCacheControl;
                        return await Task.FromResult(true);
                    }

                    return await Task.FromResult(false);
                });
        }

        public async Task<PackageStreamMetadata> UpdatePackageBlobMetadataInValidationSetAsync(PackageValidationSet validationSet)
        {
            var validationSetFileName = BuildValidationSetPackageFileName(validationSet, _fileMetadataService.FileExtension);
            return await UpdatePackageBlobMetadataAsync(validationSet, _fileMetadataService.ValidationFolderName, validationSetFileName);
        }

        public async Task<PackageStreamMetadata> UpdatePackageBlobMetadataInValidationAsync(PackageValidationSet validationSet)
        {
            var fileName = BuildFileName(validationSet, _fileMetadataService.FileSavePathTemplate, _fileMetadataService.FileExtension);
            return await UpdatePackageBlobMetadataAsync(validationSet, _fileMetadataService.ValidationFolderName, fileName);
        }

        private async Task<PackageStreamMetadata> UpdatePackageBlobMetadataAsync(PackageValidationSet validationSet, string fileFolderName, string fileName)
        {
            PackageStreamMetadata streamMetadata = null;

            // This will throw if the ETag changes between read and write operations,
            // so streamMetadata will never be null.
            await _fileStorageService.SetMetadataAsync(
                fileFolderName,
                fileName,
                async (lazyStream, metadata) =>
                {
                    var packageStream = await lazyStream.Value;
                    string hash;

                    using (_telemetryService.TrackDurationToHashPackage(
                        validationSet.PackageId,
                        validationSet.PackageNormalizedVersion,
                        validationSet.ValidationTrackingId,
                        packageStream.Length,
                        CoreConstants.Sha512HashAlgorithmId,
                        packageStream.GetType().FullName))
                    {
                        hash = CryptographyService.GenerateHash(packageStream, CoreConstants.Sha512HashAlgorithmId);
                    }

                    metadata[CoreConstants.Sha512HashAlgorithmId] = hash;

                    streamMetadata = new PackageStreamMetadata()
                    {
                        Size = packageStream.Length,
                        Hash = hash,
                        HashAlgorithm = CoreConstants.Sha512HashAlgorithmId
                    };

                    return true;
                });

            return streamMetadata;
        }

        public async Task<string> GetPublicPackageBlobETagOrNullAsync(PackageValidationSet validationSet)
        {
            var fileName = BuildFileName(validationSet,
                _fileMetadataService.FileSavePathTemplate,
                _fileMetadataService.FileExtension);

            return await _fileStorageService.GetETagOrNullAsync(_fileMetadataService.FileFolderName, fileName);
        }

        private Task<string> CopyFileAsync(
            string srcFolderName,
            string srcFileName,
            string destFolderName,
            string destFileName,
            IAccessCondition destAccessCondition)
        {
            _logger.LogInformation(
                "Copying file {SrcFolderName}/{SrcFileName} to {DestFolderName}/{DestFileName}.",
                srcFolderName,
                srcFileName,
                destFolderName,
                destFileName);

            return _fileStorageService.CopyFileAsync(
                srcFolderName,
                srcFileName,
                destFolderName,
                destFileName,
                destAccessCondition);
        }

        protected static string BuildValidationSetPackageFileName(PackageValidationSet validationSet, string extension)
        {
            return $"validation-sets/{validationSet.ValidationTrackingId}/" +
                $"{validationSet.PackageId.ToLowerInvariant()}." +
                $"{validationSet.PackageNormalizedVersion.ToLowerInvariant()}" +
                extension;
        }
    }
}