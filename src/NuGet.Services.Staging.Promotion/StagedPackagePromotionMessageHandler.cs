// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.Entities;
using NuGet.Services.ServiceBus;
using NuGet.Services.Staging;
using NuGetGallery;
using NuGetGallery.Packaging;

namespace NuGet.Services.Staging.Promotion
{
    /// <summary>
    /// Publishes validated staged package content and completes the corresponding Gallery state transition.
    /// </summary>
    public class StagedPackagePromotionMessageHandler : IMessageHandler<StagedPackagePromotionMessage>
    {
        private readonly IEntityRepository<StagedPackage> _stagedPackageRepository;
        private readonly ICorePackageService _packageService;
        private readonly IStagingBlobService _stagingBlobService;
        private readonly ICoreFileStorageService _packageFileStorageService;
        private readonly IFileMetadataService _packageFileMetadataService;
        private readonly ICoreLicenseFileService _licenseFileService;
        private readonly ICoreReadmeFileService _readmeFileService;
        private readonly ILogger<StagedPackagePromotionMessageHandler> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="StagedPackagePromotionMessageHandler"/> class.
        /// </summary>
        /// <param name="stagedPackageRepository">The staged package repository.</param>
        /// <param name="packageService">The package service.</param>
        /// <param name="stagingBlobService">The private staging blob service.</param>
        /// <param name="packageFileStorageService">The public package file storage service.</param>
        /// <param name="packageFileMetadataService">The public package file naming metadata.</param>
        /// <param name="licenseFileService">The public embedded-license file service.</param>
        /// <param name="readmeFileService">The public embedded-readme file service.</param>
        /// <param name="logger">The logger.</param>
        public StagedPackagePromotionMessageHandler(
            IEntityRepository<StagedPackage> stagedPackageRepository,
            ICorePackageService packageService,
            IStagingBlobService stagingBlobService,
            ICoreFileStorageService packageFileStorageService,
            IFileMetadataService packageFileMetadataService,
            ICoreLicenseFileService licenseFileService,
            ICoreReadmeFileService readmeFileService,
            ILogger<StagedPackagePromotionMessageHandler> logger)
        {
            _stagedPackageRepository = stagedPackageRepository ?? throw new ArgumentNullException(nameof(stagedPackageRepository));
            _packageService = packageService ?? throw new ArgumentNullException(nameof(packageService));
            _stagingBlobService = stagingBlobService ?? throw new ArgumentNullException(nameof(stagingBlobService));
            _packageFileStorageService = packageFileStorageService ?? throw new ArgumentNullException(nameof(packageFileStorageService));
            _packageFileMetadataService = packageFileMetadataService ?? throw new ArgumentNullException(nameof(packageFileMetadataService));
            _licenseFileService = licenseFileService ?? throw new ArgumentNullException(nameof(licenseFileService));
            _readmeFileService = readmeFileService ?? throw new ArgumentNullException(nameof(readmeFileService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<bool> HandleAsync(StagedPackagePromotionMessage message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            using (_logger.BeginScope("Staged package {StagedPackageKey}, promotion {PromotionId}",  message.StagedPackageKey, message.PromotionId))
            {
                _logger.LogInformation("Processing staged package promotion message.");

                var stagedPackage = _stagedPackageRepository
                    .GetAll()
                    .Include(candidate => candidate.Package.PackageRegistration.Owners)
                    .SingleOrDefault(candidate => candidate.Key == message.StagedPackageKey);
                if (!IsActivePromotionAttempt(stagedPackage, message.PromotionId))
                {
                    _logger.LogInformation("Ignoring inactive staged package promotion attempt.");
                    return true;
                }

                var package = stagedPackage.Package;
                using (_logger.BeginScope(
                    "Package {PackageId} {PackageVersion}",
                    package.PackageRegistration.Id,
                    package.NormalizedVersion))
                {
                    if (!HasValidPromotionState(stagedPackage))
                    {
                        _logger.LogWarning("Staged package has invalid promotion state. Marking promotion as failed.");
                        await MarkPromotionFailedAsync(stagedPackage);
                        return true;
                    }

                    var streamMetadata = await GetStreamMetadataAsync(stagedPackage);
                    var packageFileName = await CopyPackageAsync(package, stagedPackage);

                    try
                    {
                        _logger.LogInformation("Updating public package blob properties.");
                        await SetPackagePropertiesAsync(packageFileName);
                        await ExtractPackageContentAsync(package, stagedPackage);

                        _logger.LogInformation("Marking package as available and completing promotion in the database.");
                        await CompletePromotionAsync(package, stagedPackage, streamMetadata);
                    }
                    catch (Exception exception)
                    {
                        _logger.LogError(exception, "Failed to publish staged package. Deleting published files.");
                        await DeletePublishedFilesAsync(package, packageFileName);
                        _logger.LogInformation("Deleted published files after promotion failure.");
                        throw;
                    }

                    _logger.LogInformation("Completed staged package promotion.");
                    return true;
                }
            }
        }

        private static bool IsActivePromotionAttempt(StagedPackage stagedPackage, Guid promotionId)
        {
            return stagedPackage != null
                && stagedPackage.Status == StagedPackageStatus.Promoting
                && stagedPackage.ActivePromotionId == promotionId
                && stagedPackage.Package.PackageStatusKey == PackageStatus.Staged;
        }

        private static bool HasValidPromotionState(StagedPackage stagedPackage)
        {
            return !string.IsNullOrWhiteSpace(stagedPackage.ValidatedBlobPath)
                && !string.IsNullOrWhiteSpace(stagedPackage.ValidatedBlobETag)
                && stagedPackage.Package.PackageRegistration.Owners.Any(owner => owner.Key == stagedPackage.OwnerKey);
        }

        private async Task MarkPromotionFailedAsync(StagedPackage stagedPackage)
        {
            stagedPackage.Status = StagedPackageStatus.PromotionFailed;
            await _stagedPackageRepository.CommitChangesAsync();
        }

        private async Task<PackageStreamMetadata> GetStreamMetadataAsync(StagedPackage stagedPackage)
        {
            _logger.LogInformation("Calculating validated package stream metadata.");
            using (var packageStream = await _stagingBlobService.OpenPackageFileAsync(stagedPackage.ValidatedBlobPath, stagedPackage.ValidatedBlobETag))
            {
                return new PackageStreamMetadata
                {
                    HashAlgorithm = CoreConstants.Sha512HashAlgorithmId,
                    Hash = CryptographyService.GenerateHash(packageStream, CoreConstants.Sha512HashAlgorithmId),
                    Size = packageStream.Length,
                };
            }
        }

        private async Task<string> CopyPackageAsync(Package package, StagedPackage stagedPackage)
        {
            _logger.LogInformation("Copying validated package to public storage.");
            var sourceUri = await _stagingBlobService.GetPackageReadUriAsync(stagedPackage.ValidatedBlobPath, stagedPackage.ValidatedBlobETag);

            var packageFileName = FileNameHelper.BuildFileName(
                package.PackageRegistration.Id,
                package.NormalizedVersion,
                _packageFileMetadataService.FileSavePathTemplate,
                _packageFileMetadataService.FileExtension);

            await _packageFileStorageService.CopyFileAsync(
                sourceUri,
                _packageFileMetadataService.FileFolderName,
                packageFileName,
                AccessConditionWrapper.GenerateIfNotExistsCondition());

            return packageFileName;
        }

        private Task SetPackagePropertiesAsync(string packageFileName)
        {
            return _packageFileStorageService.SetPropertiesAsync(
                _packageFileMetadataService.FileFolderName,
                packageFileName,
                (_, properties) =>
                {
                    if (!string.Equals(properties.CacheControl, CoreConstants.DefaultCacheControl, StringComparison.OrdinalIgnoreCase))
                    {
                        properties.CacheControl = CoreConstants.DefaultCacheControl;
                        return Task.FromResult(true);
                    }

                    return Task.FromResult(false);
                });
        }

        private async Task ExtractPackageContentAsync(Package package, StagedPackage stagedPackage)
        {
            if (package.EmbeddedLicenseType == EmbeddedLicenseFileType.Absent && !package.HasEmbeddedReadme)
            {
                _logger.LogInformation("Package has no embedded license or readme to extract.");
                return;
            }

            using (var packageStream = await _stagingBlobService.OpenPackageFileAsync(stagedPackage.ValidatedBlobPath, stagedPackage.ValidatedBlobETag))
            {
                if (package.EmbeddedLicenseType != EmbeddedLicenseFileType.Absent)
                {
                    _logger.LogInformation("Extracting embedded license.");
                    await _licenseFileService.ExtractAndSaveLicenseFileAsync(package, packageStream);
                    _logger.LogInformation("Extracted embedded license.");
                }

                if (package.HasEmbeddedReadme)
                {
                    _logger.LogInformation("Extracting embedded readme.");
                    await _readmeFileService.ExtractAndSaveReadmeFileAsync(package, packageStream);
                    _logger.LogInformation("Extracted embedded readme.");
                }
            }
        }

        private async Task CompletePromotionAsync(Package package, StagedPackage stagedPackage, PackageStreamMetadata streamMetadata)
        {
            await _stagedPackageRepository.ExecuteInTransactionAsync(async () =>
            {
                await _packageService.UpdatePackageStreamMetadataAsync(package, streamMetadata, commitChanges: false);
                await _packageService.UpdatePackageStatusAsync(package, PackageStatus.Available, commitChanges: false);
                _stagedPackageRepository.DeleteOnCommit(stagedPackage);
                await _stagedPackageRepository.CommitChangesAsync();
            });
        }

        private async Task DeletePublishedFilesAsync(Package package, string packageFileName)
        {
            await _packageFileStorageService.DeleteFileAsync(_packageFileMetadataService.FileFolderName, packageFileName);

            if (package.EmbeddedLicenseType != EmbeddedLicenseFileType.Absent)
            {
                await _licenseFileService.DeleteLicenseFileAsync(package.Id, package.NormalizedVersion);
            }

            if (package.HasEmbeddedReadme)
            {
                await _readmeFileService.DeleteReadmeFileAsync(package.Id, package.NormalizedVersion);
            }
        }
    }
}
