// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Services.Entities;
using NuGet.Services.Validation.Orchestrator.Telemetry;
using NuGetGallery;
using NuGetGallery.Packaging;

namespace NuGet.Services.Validation.Orchestrator
{
    public class PackageStatusProcessor : EntityStatusProcessor<Package>
    {
        private readonly ICoreLicenseFileService _coreLicenseFileService;

        private readonly SasDefinitionConfiguration _sasDefinitionConfiguration;

        private readonly ICoreReadmeFileService _coreReadmeFileService;

        private readonly IEntitiesContext _entitiesContext;

        private readonly IStagingBlobService _stagingBlobService;

        public PackageStatusProcessor(
            IEntityService<Package> galleryPackageService,
            IValidationFileService packageFileService,
            IValidatorProvider validatorProvider,
            ITelemetryService telemetryService,
            IOptionsSnapshot<SasDefinitionConfiguration> options,
            ILogger<EntityStatusProcessor<Package>> logger,
            ICoreLicenseFileService coreLicenseFileService,
            ICoreReadmeFileService coreReadmeFileService,
            IEntitiesContext entitiesContext,
            IStagingBlobService stagingBlobService)
            : base(galleryPackageService, packageFileService, validatorProvider, telemetryService, logger)
        {
            _coreLicenseFileService = coreLicenseFileService ?? throw new ArgumentNullException(nameof(coreLicenseFileService));
            _sasDefinitionConfiguration = (options == null || options.Value == null) ? new SasDefinitionConfiguration() : options.Value;
            _coreReadmeFileService = coreReadmeFileService ?? throw new ArgumentNullException(nameof(coreReadmeFileService));
            _entitiesContext = entitiesContext ?? throw new ArgumentNullException(nameof(entitiesContext));
            _stagingBlobService = stagingBlobService ?? throw new ArgumentNullException(nameof(stagingBlobService));
        }

        protected override Task ApplyStagedValidationStatusAsync(
            IValidatingEntity<Package> validatingEntity,
            PackageValidationSet validationSet,
            StagedPackageStatus status)
        {
            switch (status)
            {
                case StagedPackageStatus.Ready:
                    return MarkStagedPackageReadyAsync(validatingEntity, validationSet);
                case StagedPackageStatus.FailedValidation:
                    return MarkStagedPackageFailedAsync(validatingEntity, validationSet);
                default:
                    throw new ArgumentOutOfRangeException(nameof(status));
            }
        }

        private async Task MarkStagedPackageReadyAsync(IValidatingEntity<Package> validatingEntity, PackageValidationSet validationSet)
        {
            var stagedPackage = GetCurrentStagedPackage(validatingEntity.Key, validationSet.ValidationTrackingId, validationSet.PackageETag);
            if (stagedPackage == null)
            {
                return;
            }

            StagingFileReference file;
            using (var packageFile = await _packageFileService.DownloadValidationSetPackageFileAsync(validationSet))
            {
                file = await _stagingBlobService.SavePackageFileAsync(validationSet.PackageId, validationSet.PackageNormalizedVersion, packageFile);
            }

            stagedPackage = GetCurrentStagedPackage(validatingEntity.Key, validationSet.ValidationTrackingId, validationSet.PackageETag);
            if (stagedPackage == null)
            {
                return;
            }

            await _galleryPackageService.UpdateMetadataAsync(
                validatingEntity.EntityRecord,
                new PackageStreamMetadata
                {
                    HashAlgorithm = CoreConstants.Sha512HashAlgorithmId,
                    Hash = file.ContentHash,
                    Size = file.Length,
                },
                commitChanges: false);

            stagedPackage.BlobPath = file.Path;
            stagedPackage.BlobETag = file.ETag;
            stagedPackage.Status = StagedPackageStatus.Ready;
            await TrySaveStagedPackageAsync(validationSet);
        }

        private async Task MarkStagedPackageFailedAsync(IValidatingEntity<Package> validatingEntity, PackageValidationSet validationSet)
        {
            var stagedPackage = GetCurrentStagedPackage(validatingEntity.Key, validationSet.ValidationTrackingId, validationSet.PackageETag);
            if (stagedPackage == null)
            {
                return;
            }

            stagedPackage.Status = StagedPackageStatus.FailedValidation;
            await TrySaveStagedPackageAsync(validationSet);
        }

        private StagedPackage GetCurrentStagedPackage(int packageKey, Guid validationTrackingId, string blobETag)
        {
            return _entitiesContext.StagedPackages.SingleOrDefault(stagedPackage =>
                stagedPackage.PackageKey == packageKey &&
                stagedPackage.ValidationTrackingId == validationTrackingId &&
                stagedPackage.BlobETag == blobETag &&
                stagedPackage.Status == StagedPackageStatus.Validating);
        }

        private async Task TrySaveStagedPackageAsync(PackageValidationSet validationSet)
        {
            try
            {
                await _entitiesContext.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                _logger.LogInformation(
                    "Ignoring stale staged package outcome for package key {PackageKey}, validation set {ValidationTrackingId}.",
                    validationSet.PackageKey,
                    validationSet.ValidationTrackingId);
            }
        }

        protected override async Task OnBeforeUpdateDatabaseToMakePackageAvailable(
            IValidatingEntity<Package> validatingEntity,
            PackageValidationSet validationSet)
        {
            if (validatingEntity.EntityRecord.EmbeddedLicenseType != EmbeddedLicenseFileType.Absent || validatingEntity.EntityRecord.HasEmbeddedReadme)
            {
                using (_telemetryService.TrackDurationToExtractLicenseAndReadmeFile(validationSet.PackageId, validationSet.PackageNormalizedVersion, validationSet.ValidationTrackingId.ToString()))
                using (var packageStream = await _packageFileService.DownloadPackageFileToDiskAsync(validationSet, _sasDefinitionConfiguration.PackageStatusProcessorSasDefinition))
                {
                    if (validatingEntity.EntityRecord.EmbeddedLicenseType != EmbeddedLicenseFileType.Absent)
                    {
                        _logger.LogInformation("Extracting the license file of type {EmbeddedLicenseFileType} for the package {PackageId} {PackageVersion}",
                            validatingEntity.EntityRecord.EmbeddedLicenseType,
                            validationSet.PackageId,
                            validationSet.PackageNormalizedVersion);
                        await _coreLicenseFileService.ExtractAndSaveLicenseFileAsync(validatingEntity.EntityRecord, packageStream);
                        _logger.LogInformation("Successfully extracted the license file.");
                    }

                    if (validatingEntity.EntityRecord.HasEmbeddedReadme)
                    {
                        _logger.LogInformation("Extracting the readme file of type {EmbeddedReadmeType} for the package {PackageId} {PackageVersion}",
                            validatingEntity.EntityRecord.EmbeddedReadmeType,
                            validationSet.PackageId,
                            validationSet.PackageNormalizedVersion);
                        await _coreReadmeFileService.ExtractAndSaveReadmeFileAsync(validatingEntity.EntityRecord, packageStream);
                        _logger.LogInformation("Successfully extracted the readme file.");
                    }
                }
            }
        }

        protected override async Task OnCleanupAfterDatabaseUpdateFailure(
            IValidatingEntity<Package> validatingEntity,
            PackageValidationSet validationSet)
        {
            if (validatingEntity.EntityRecord.EmbeddedLicenseType != EmbeddedLicenseFileType.Absent)
            {
                using (_telemetryService.TrackDurationToDeleteLicenseFile(validationSet.PackageId, validationSet.PackageNormalizedVersion, validationSet.ValidationTrackingId.ToString()))
                {
                    _logger.LogInformation("Cleaning up the license file for the package {PackageId} {PackageVersion}", validationSet.PackageId, validationSet.PackageNormalizedVersion);
                    await _coreLicenseFileService.DeleteLicenseFileAsync(validationSet.PackageId, validationSet.PackageNormalizedVersion);
                    _logger.LogInformation("Deleted the license file for the package {PackageId} {PackageVersion}", validationSet.PackageId, validationSet.PackageNormalizedVersion);
                }
            }

            if (validatingEntity.EntityRecord.HasEmbeddedReadme)
            {
                using (_telemetryService.TrackDurationToDeleteReadmeFile(validationSet.PackageId, validationSet.PackageNormalizedVersion, validationSet.ValidationTrackingId.ToString()))
                {
                    _logger.LogInformation("Cleaning up the readme file for the package {PackageId} {PackageVersion}", validationSet.PackageId, validationSet.PackageNormalizedVersion);
                    await _coreReadmeFileService.DeleteReadmeFileAsync(validationSet.PackageId, validationSet.PackageNormalizedVersion);
                    _logger.LogInformation("Deleted the readme file for the package {PackageId} {PackageVersion}", validationSet.PackageId, validationSet.PackageNormalizedVersion);
                }
            }
        }
    }
}
