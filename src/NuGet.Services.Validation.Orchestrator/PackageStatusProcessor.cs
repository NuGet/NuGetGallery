// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Services.Entities;
using NuGet.Services.Validation.Orchestrator.Telemetry;
using NuGetGallery;

namespace NuGet.Services.Validation.Orchestrator
{
    public class PackageStatusProcessor : EntityStatusProcessor<Package>
    {
        private readonly ICoreLicenseFileService _coreLicenseFileService;
        private readonly SasDefinitionConfiguration _sasDefinitionConfiguration;
        private readonly ICoreReadmeFileService _coreReadmeFileService;

        public PackageStatusProcessor(
            IEntityService<Package> galleryPackageService,
            IValidationFileService packageFileService,
            IValidatorProvider validatorProvider,
            ITelemetryService telemetryService,
            IOptionsSnapshot<SasDefinitionConfiguration> options,
            ILogger<EntityStatusProcessor<Package>> logger,
            ICoreLicenseFileService coreLicenseFileService,
            ICoreReadmeFileService coreReadmeFileService) 
            : base(galleryPackageService, packageFileService, validatorProvider, telemetryService, logger)
        {
            _coreLicenseFileService = coreLicenseFileService ?? throw new ArgumentNullException(nameof(coreLicenseFileService));
            _sasDefinitionConfiguration = (options == null || options.Value == null) ? new SasDefinitionConfiguration() : options.Value;
            _coreReadmeFileService = coreReadmeFileService ?? throw new ArgumentNullException(nameof(coreReadmeFileService));
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
