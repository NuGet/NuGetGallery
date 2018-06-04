// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Jobs.Validation;
using NuGet.Services.Validation.Orchestrator.Telemetry;
using NuGetGallery;
using NuGetGallery.Packaging;

namespace NuGet.Services.Validation.Orchestrator
{
    public class ValidationPackageFileService : CorePackageFileService, IValidationPackageFileService
    {
        /// <summary>
        /// The value picked today is based off of the maximum duration we wait when downloading packages using the
        /// <see cref="IPackageDownloader"/>.
        /// </summary>
        private static readonly TimeSpan AccessDuration = TimeSpan.FromMinutes(10);

        private readonly ICoreFileStorageService _fileStorageService;
        private readonly IPackageDownloader _packageDownloader;
        private readonly ITelemetryService _telemetryService;
        private readonly ILogger<ValidationPackageFileService> _logger;

        public ValidationPackageFileService(
            ICoreFileStorageService fileStorageService,
            IPackageDownloader packageDownloader,
            ITelemetryService telemetryService,
            ILogger<ValidationPackageFileService> logger) : base(fileStorageService)
        {
            _fileStorageService = fileStorageService ?? throw new ArgumentNullException(nameof(fileStorageService));
            _packageDownloader = packageDownloader ?? throw new ArgumentNullException(nameof(packageDownloader));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<Stream> DownloadPackageFileToDiskAsync(Package package)
        {
            var packageUri = await GetPackageReadUriAsync(package);

            return await _packageDownloader.DownloadAsync(packageUri, CancellationToken.None);
        }

        public Task CopyValidationPackageForValidationSetAsync(PackageValidationSet validationSet)
        {
            var srcFileName = BuildFileName(
                validationSet.PackageId,
                validationSet.PackageNormalizedVersion,
                CoreConstants.PackageFileSavePathTemplate,
                CoreConstants.NuGetPackageFileExtension);

            return CopyFileAsync(
                CoreConstants.ValidationFolderName,
                srcFileName,
                CoreConstants.ValidationFolderName,
                BuildValidationSetPackageFileName(validationSet),
                AccessConditionWrapper.GenerateEmptyCondition());
        }

        public async Task BackupPackageFileFromValidationSetPackageAsync(Package package, PackageValidationSet validationSet)
        {
            _logger.LogInformation(
                "Backing up package for validation set {ValidationTrackingId} ({PackageId} {PackageVersion}).",
                validationSet.ValidationTrackingId,
                validationSet.PackageId,
                validationSet.PackageNormalizedVersion);

            var packageUri = await GetPackageForValidationSetReadUriAsync(
                validationSet,
                DateTimeOffset.UtcNow.Add(AccessDuration));

            using (var packageStream = await _packageDownloader.DownloadAsync(packageUri, CancellationToken.None))
            {
                await StorePackageFileInBackupLocationAsync(package, packageStream);
            }
        }

        public Task<string> CopyPackageFileForValidationSetAsync(PackageValidationSet validationSet)
        {
            var srcFileName = BuildFileName(
                validationSet.PackageId,
                validationSet.PackageNormalizedVersion,
                CoreConstants.PackageFileSavePathTemplate,
                CoreConstants.NuGetPackageFileExtension);

            return CopyFileAsync(
                CoreConstants.PackagesFolderName,
                srcFileName,
                CoreConstants.ValidationFolderName,
                BuildValidationSetPackageFileName(validationSet),
                AccessConditionWrapper.GenerateEmptyCondition());
        }

        public Task CopyValidationPackageToPackageFileAsync(string id, string normalizedVersion)
        {
            var fileName = BuildFileName(
                id,
                normalizedVersion,
                CoreConstants.PackageFileSavePathTemplate,
                CoreConstants.NuGetPackageFileExtension);

            return CopyFileAsync(
                CoreConstants.ValidationFolderName,
                fileName,
                CoreConstants.PackagesFolderName,
                fileName,
                AccessConditionWrapper.GenerateIfNotExistsCondition());
        }

        public Task CopyValidationSetPackageToPackageFileAsync(
            PackageValidationSet validationSet,
            IAccessCondition destAccessCondition)
        {
            var srcFileName = BuildValidationSetPackageFileName(validationSet);

            var destFileName = BuildFileName(
                validationSet.PackageId,
                validationSet.PackageNormalizedVersion,
                CoreConstants.PackageFileSavePathTemplate,
                CoreConstants.NuGetPackageFileExtension);

            return CopyFileAsync(
                CoreConstants.ValidationFolderName,
                srcFileName,
                CoreConstants.PackagesFolderName,
                destFileName,
                destAccessCondition);
        }

        public Task<bool> DoesValidationSetPackageExistAsync(PackageValidationSet validationSet)
        {
            var fileName = BuildValidationSetPackageFileName(validationSet);
            
            return _fileStorageService.FileExistsAsync(CoreConstants.ValidationFolderName, fileName);
        }

        public Task DeletePackageForValidationSetAsync(PackageValidationSet validationSet)
        {
            var fileName = BuildValidationSetPackageFileName(validationSet);

            _logger.LogInformation(
                "Deleting package for validation set {ValidationTrackingId} from {FolderName}/{FileName}.",
                validationSet.ValidationTrackingId,
                CoreConstants.ValidationFolderName,
                fileName);

            return _fileStorageService.DeleteFileAsync(CoreConstants.ValidationFolderName, fileName);
        }

        public Task<Uri> GetPackageForValidationSetReadUriAsync(PackageValidationSet validationSet, DateTimeOffset endOfAccess)
        {
            var fileName = BuildValidationSetPackageFileName(validationSet);

            return _fileStorageService.GetFileReadUriAsync(CoreConstants.ValidationFolderName, fileName, endOfAccess);
        }

        public Task CopyPackageUrlForValidationSetAsync(PackageValidationSet validationSet, string srcPackageUrl)
        {
            var destFileName = BuildValidationSetPackageFileName(validationSet);

            _logger.LogInformation(
                "Copying URL {SrcPackageUrl} to {DestFolderName}/{DestFileName}.",
                srcPackageUrl,
                CoreConstants.ValidationFolderName,
                srcPackageUrl);

            return _fileStorageService.CopyFileAsync(
                new Uri(srcPackageUrl),
                CoreConstants.ValidationFolderName,
                destFileName,
                AccessConditionWrapper.GenerateEmptyCondition());
        }

        public async Task<PackageStreamMetadata> UpdatePackageBlobMetadataAsync(Package package)
        {
            var fileName = BuildFileName(
                package,
                CoreConstants.PackageFileSavePathTemplate,
                CoreConstants.NuGetPackageFileExtension);

            PackageStreamMetadata streamMetadata = null;

            // This will throw if the ETag changes between read and write operations,
            // so streamMetadata will never be null.
            await _fileStorageService.SetMetadataAsync(
                CoreConstants.PackagesFolderName,
                fileName,
                async (lazyStream, metadata) =>
                {
                    var packageStream = await lazyStream.Value;
                    string hash;

                    using (_telemetryService.TrackDurationToHashPackage(
                        package.PackageRegistration.Id,
                        package.NormalizedVersion,
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

        private static string BuildValidationSetPackageFileName(PackageValidationSet validationSet)
        {
            return $"validation-sets/{validationSet.ValidationTrackingId}/" +
                $"{validationSet.PackageId.ToLowerInvariant()}." +
                $"{validationSet.PackageNormalizedVersion.ToLowerInvariant()}" +
                CoreConstants.NuGetPackageFileExtension;
        }
    }
}