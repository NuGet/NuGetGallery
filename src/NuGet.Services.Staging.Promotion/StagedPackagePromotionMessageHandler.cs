// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Services.Entities;
using NuGet.Services.ServiceBus;
using NuGetGallery;
using NuGetGallery.Packaging;

namespace NuGet.Services.Staging.Promotion
{
    public class StagedPackagePromotionMessageHandler : IMessageHandler<StagedPackagePromotionMessage>
    {
        private readonly IEntityRepository<StagedPackage> _stagedPackageRepository;
        private readonly ICorePackageService _packageService;
        private readonly IStagingBlobService _stagingBlobService;
        private readonly ICoreFileStorageService _packageFileStorageService;
        private readonly IFileMetadataService _packageFileMetadataService;
        private readonly ICoreLicenseFileService _licenseFileService;
        private readonly ICoreReadmeFileService _readmeFileService;

        public StagedPackagePromotionMessageHandler(
            IEntityRepository<StagedPackage> stagedPackageRepository,
            ICorePackageService packageService,
            IStagingBlobService stagingBlobService,
            ICoreFileStorageService packageFileStorageService,
            IFileMetadataService packageFileMetadataService,
            ICoreLicenseFileService licenseFileService,
            ICoreReadmeFileService readmeFileService)
        {
            _stagedPackageRepository = stagedPackageRepository ?? throw new ArgumentNullException(nameof(stagedPackageRepository));
            _packageService = packageService ?? throw new ArgumentNullException(nameof(packageService));
            _stagingBlobService = stagingBlobService ?? throw new ArgumentNullException(nameof(stagingBlobService));
            _packageFileStorageService = packageFileStorageService ?? throw new ArgumentNullException(nameof(packageFileStorageService));
            _packageFileMetadataService = packageFileMetadataService ?? throw new ArgumentNullException(nameof(packageFileMetadataService));
            _licenseFileService = licenseFileService ?? throw new ArgumentNullException(nameof(licenseFileService));
            _readmeFileService = readmeFileService ?? throw new ArgumentNullException(nameof(readmeFileService));
        }

        public async Task<bool> HandleAsync(StagedPackagePromotionMessage message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            var stagedPackage = _stagedPackageRepository
                .GetAll()
                .Include(candidate => candidate.Package.PackageRegistration.Owners)
                .SingleOrDefault(candidate => candidate.Key == message.StagedPackageKey);
            if (!CanPromote(stagedPackage, message.PromotionId))
            {
                return true;
            }

            var package = stagedPackage.Package;
            var streamMetadata = await GetStreamMetadataAsync(stagedPackage);
            var packageFileName = await CopyPackageAsync(package, stagedPackage);

            try
            {
                await SetPackagePropertiesAsync(packageFileName);
                await ExtractPackageContentAsync(package, stagedPackage);
                await CompletePromotionAsync(package, stagedPackage, streamMetadata);
            }
            catch
            {
                await DeletePublishedFilesAsync(package, packageFileName);
                throw;
            }

            return true;
        }

        private static bool CanPromote(StagedPackage stagedPackage, Guid promotionId)
        {
            return stagedPackage != null
                && stagedPackage.Status == StagedPackageStatus.Promoting
                && stagedPackage.ActivePromotionId == promotionId
                && stagedPackage.Package.PackageStatusKey == PackageStatus.Staged
                && !string.IsNullOrWhiteSpace(stagedPackage.ValidatedBlobPath)
                && !string.IsNullOrWhiteSpace(stagedPackage.ValidatedBlobETag)
                && stagedPackage.Package.PackageRegistration.Owners.Any(owner => owner.Key == stagedPackage.OwnerKey);
        }

        private async Task<PackageStreamMetadata> GetStreamMetadataAsync(StagedPackage stagedPackage)
        {
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
                return;
            }

            using (var packageStream = await _stagingBlobService.OpenPackageFileAsync(stagedPackage.ValidatedBlobPath, stagedPackage.ValidatedBlobETag))
            {
                if (package.EmbeddedLicenseType != EmbeddedLicenseFileType.Absent)
                {
                    await _licenseFileService.ExtractAndSaveLicenseFileAsync(package, packageStream);
                }

                if (package.HasEmbeddedReadme)
                {
                    await _readmeFileService.ExtractAndSaveReadmeFileAsync(package, packageStream);
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
