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
    public class StagedPackageValidationSetProvider : ValidationSetProvider<StagedPackage>
    {
        private readonly IValidationFileService _packageFileService;
        private readonly IStagingBlobService _stagingBlobService;
        private readonly IValidationStorageService _validationStorageService;
        private readonly ILogger<ValidationSetProvider<StagedPackage>> _logger;

        public StagedPackageValidationSetProvider(
            IValidationStorageService validationStorageService,
            IValidationFileService packageFileService,
            IStagingBlobService stagingBlobService,
            IValidatorProvider validatorProvider,
            IOptionsSnapshot<ValidationConfiguration> validationConfigurationAccessor,
            IOptionsSnapshot<SasDefinitionConfiguration> sasDefinitionConfigurationAccessor,
            ITelemetryService telemetryService,
            ILogger<ValidationSetProvider<StagedPackage>> logger)
            : base(
                  validationStorageService,
                  packageFileService,
                  validatorProvider,
                  validationConfigurationAccessor,
                  sasDefinitionConfigurationAccessor,
                  telemetryService,
                  logger)
        {
            _packageFileService = packageFileService ?? throw new ArgumentNullException(nameof(packageFileService));
            _stagingBlobService = stagingBlobService ?? throw new ArgumentNullException(nameof(stagingBlobService));
            _validationStorageService = validationStorageService;
            _logger = logger;
        }

        public override async Task<PackageValidationSet> TryGetOrCreateValidationSetAsync(ProcessValidationSetData message, IValidatingEntity<StagedPackage> validatingEntity)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            if (validatingEntity == null)
            {
                throw new ArgumentNullException(nameof(validatingEntity));
            }

            var status = validatingEntity.EntityRecord.Status;
            if (status == StagedPackageStatus.Deleted || status == StagedPackageStatus.Superseded)
            {
                var existing = await _validationStorageService.GetValidationSetAsync(message.ValidationTrackingId);
                if (existing != null && existing.PackageKey != validatingEntity.Key)
                {
                    throw new InvalidOperationException($"Validation set key ({existing.PackageKey}) does not match expected staged package key ({validatingEntity.Key}).");
                }

                _logger.LogInformation("Retired staged attempt {AttemptKey} may finish existing validation but will not create a new set.", validatingEntity.Key);
                return existing;
            }

            return await base.TryGetOrCreateValidationSetAsync(message, validatingEntity);
        }

        protected override async Task CopyPackageFileToValidationSetAsync(PackageValidationSet validationSet, IValidatingEntity<StagedPackage> validatingEntity)
        {
            var stagedPackage = validatingEntity.EntityRecord;
            var packageUri = await _stagingBlobService.GetPackageReadUriAsync(stagedPackage.UploadedBlobPath, stagedPackage.UploadedBlobETag);

            await _packageFileService.CopyPackageUrlForValidationSetAsync(validationSet, packageUri.AbsoluteUri);

            validationSet.PackageETag = stagedPackage.UploadedBlobETag;
        }
    }
}
