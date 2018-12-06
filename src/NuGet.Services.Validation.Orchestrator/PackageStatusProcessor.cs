// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.Entities;
using NuGet.Services.Validation.Orchestrator.Telemetry;
using NuGetGallery;

namespace NuGet.Services.Validation.Orchestrator
{
    public class PackageStatusProcessor : EntityStatusProcessor<Package>
    {
        private readonly ICoreLicenseFileService _coreLicenseFileService;

        public PackageStatusProcessor(
            IEntityService<Package> galleryPackageService,
            IValidationFileService packageFileService,
            IValidatorProvider validatorProvider,
            ITelemetryService telemetryService,
            ILogger<EntityStatusProcessor<Package>> logger,
            ICoreLicenseFileService coreLicenseFileService) 
            : base(galleryPackageService, packageFileService, validatorProvider, telemetryService, logger)
        {
            _coreLicenseFileService = coreLicenseFileService ?? throw new ArgumentNullException(nameof(coreLicenseFileService));
        }

        protected override async Task OnBeforeUpdateDatabaseToMakePackageAvailable(
            IValidatingEntity<Package> validatingEntity,
            PackageValidationSet validationSet)
        {
            if (validatingEntity.EntityRecord.EmbeddedLicenseType != EmbeddedLicenseFileType.Absent)
            {
                using (_telemetryService.TrackDurationToExtractLicenseFile(validationSet.PackageId, validationSet.PackageNormalizedVersion, validationSet.ValidationTrackingId.ToString()))
                using (var packageStream = await _packageFileService.DownloadPackageFileToDiskAsync(validationSet))
                {
                    _logger.LogInformation("Extracting the license file of type {EmbeddedLicenseFileType} for the package {PackageId} {PackageVersion}",
                        validatingEntity.EntityRecord.EmbeddedLicenseType,
                        validationSet.PackageId,
                        validationSet.PackageNormalizedVersion);
                    await _coreLicenseFileService.ExtractAndSaveLicenseFileAsync(validatingEntity.EntityRecord, packageStream);
                    _logger.LogInformation("Successfully extracted the license file.");
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
        }
    }
}
