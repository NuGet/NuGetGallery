// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Jobs.Validation;
using NuGet.Services.Entities;
using NuGet.Services.Staging;
using NuGet.Services.Validation.Orchestrator.Telemetry;
using NuGetGallery;

namespace NuGet.Services.Validation.Orchestrator
{
    /// <summary>
    /// Creates validation or ingestion sets from the current immutable staged symbol upload.
    /// </summary>
    public class StagedSymbolPackageValidationSetProvider : ValidationSetProvider<StagedSymbolPackage>
    {
        private readonly IValidationFileService _fileService;
        private readonly IStagingBlobService _stagingBlobService;
        private readonly ValidationConfiguration _configuration;
        private readonly ILogger<ValidationSetProvider<StagedSymbolPackage>> _logger;

        public StagedSymbolPackageValidationSetProvider(
            IValidationStorageService validationStorageService,
            IValidationFileService fileService,
            IStagingBlobService stagingBlobService,
            IValidatorProvider validatorProvider,
            IOptionsSnapshot<ValidationConfiguration> validationConfigurationAccessor,
            IOptionsSnapshot<SasDefinitionConfiguration> sasDefinitionConfigurationAccessor,
            ITelemetryService telemetryService,
            ILogger<ValidationSetProvider<StagedSymbolPackage>> logger)
            : base(
                  validationStorageService,
                  fileService,
                  validatorProvider,
                  validationConfigurationAccessor,
                  sasDefinitionConfigurationAccessor,
                  telemetryService,
                  logger)
        {
            _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
            _stagingBlobService = stagingBlobService ?? throw new ArgumentNullException(nameof(stagingBlobService));
            _configuration = validationConfigurationAccessor.Value;
            _logger = logger;
        }

        public override async Task<PackageValidationSet> TryGetOrCreateValidationSetAsync(ProcessValidationSetData message, IValidatingEntity<StagedSymbolPackage> validatingEntity)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            if (validatingEntity == null)
            {
                throw new ArgumentNullException(nameof(validatingEntity));
            }

            var attempt = validatingEntity.EntityRecord;
            if (!CanProcessAttempt(message, validatingEntity))
            {
                _logger.LogInformation("Ignoring inactive staged symbol request {ValidationTrackingId} for attempt {AttemptKey}.",
                    message.ValidationTrackingId, attempt.Key);
                return null;
            }

            var isPromotion = attempt.Status == StagedPackageStatus.Promoting;
            if (isPromotion)
            {
                if (!_configuration.EnableStagedSymbolPromotion)
                {
                    throw new NotSupportedException("Staged symbol promotion is not enabled.");
                }

                if (attempt.StagedPackageIdentity.StagingGroupKey.HasValue)
                {
                    throw new NotSupportedException("Grouped symbol promotion is not enabled yet.");
                }
            }

            var validationSet = await base.TryGetOrCreateValidationSetAsync(message, validatingEntity);
            if (validationSet == null)
            {
                return null;
            }

            EnsureMatchesAttempt(validationSet, attempt, isPromotion);
            return validationSet;
        }

        protected override IEnumerable<ValidationConfigurationItem> GetValidationsToStart(IValidatingEntity<StagedSymbolPackage> validatingEntity)
        {
            if (validatingEntity.EntityRecord.Status == StagedPackageStatus.Promoting)
            {
                return new[] { SymbolPromotionValidationConfiguration.Create(_configuration) };
            }

            var validations = base.GetValidationsToStart(validatingEntity)
                .Where(IsStagedSymbolValidator)
                .ToList();

            EnsureValidConfiguration(validations);

            return validations;
        }

        protected override TimeSpan GetDeduplicationWindow(IValidatingEntity<StagedSymbolPackage> validatingEntity)
        {
            if (validatingEntity.EntityRecord.Status == StagedPackageStatus.Promoting)
            {
                // A recent scan/validation set must not suppress the accepted ingestion phase.
                return TimeSpan.Zero;
            }

            return base.GetDeduplicationWindow(validatingEntity);
        }

        protected override async Task CopyPackageFileToValidationSetAsync(PackageValidationSet validationSet, IValidatingEntity<StagedSymbolPackage> validatingEntity)
        {
            var attempt = validatingEntity.EntityRecord;
            var uri = await _stagingBlobService.GetPackageReadUriAsync(attempt.UploadedBlobPath, attempt.UploadedBlobETag);
            await _fileService.CopyPackageUrlForValidationSetAsync(validationSet, uri.AbsoluteUri);
            validationSet.PackageETag = attempt.UploadedBlobETag;
        }

        private static bool CanProcessAttempt(ProcessValidationSetData message, IValidatingEntity<StagedSymbolPackage> validatingEntity)
        {
            var attempt = validatingEntity.EntityRecord;
            var identity = attempt.StagedPackageIdentity;
            var package = identity.Package;
            var matchesEntity =
                message.ValidatingType == ValidatingType.StagedSymbolPackage &&
                message.EntityKey == attempt.Key &&
                validatingEntity.ValidatingType == message.ValidatingType &&
                validatingEntity.Key == attempt.Key;
            var matchesPackage =
                string.Equals(message.PackageId, package.PackageRegistration.Id, StringComparison.OrdinalIgnoreCase) &&
                message.PackageNormalizedVersion == package.NormalizedVersion;
            var isCurrentAttempt = identity.CurrentStagedSymbolPackageKey == attempt.Key;

            if (!matchesEntity || !matchesPackage || !isCurrentAttempt)
            {
                return false;
            }

            if (attempt.Status == StagedPackageStatus.Promoting)
            {
                return CanPromoteAttempt(message.ValidationTrackingId, attempt);
            }

            var parentIsAvailable = package.PackageStatusKey == PackageStatus.Available;
            var parentIsStaged = package.PackageStatusKey == PackageStatus.Staged && identity.CurrentStagedPackageKey.HasValue;

            return attempt.Status == StagedPackageStatus.Validating && (parentIsAvailable || parentIsStaged);
        }

        private static bool CanPromoteAttempt(Guid trackingId, StagedSymbolPackage attempt)
        {
            if (!attempt.ActivePromotionId.HasValue)
            {
                return false;
            }

            var identity = attempt.StagedPackageIdentity;
            var package = identity.Package;
            var matchesPromotion = trackingId == SymbolPromotionValidationTrackingId.Create(attempt.ActivePromotionId.Value, attempt.Key);
            var parentIsAvailable = package.PackageStatusKey == PackageStatus.Available;
            var symbolIsStaged = attempt.SymbolPackage.StatusKey == PackageStatus.Staged;
            var ownerStillOwnsPackage = package.PackageRegistration.Owners.Any(owner => owner.Key == identity.OwnerKey);

            return matchesPromotion && parentIsAvailable && symbolIsStaged && ownerStillOwnsPackage;
        }

        private static void EnsureMatchesAttempt(PackageValidationSet validationSet, StagedSymbolPackage attempt, bool isPromotion)
        {
            var package = attempt.StagedPackageIdentity.Package;
            var matchesType = validationSet.ValidatingType == ValidatingType.StagedSymbolPackage;
            var matchesUpload = validationSet.PackageETag == attempt.UploadedBlobETag;
            var matchesPackage =
                string.Equals(validationSet.PackageId, package.Id, StringComparison.OrdinalIgnoreCase) &&
                validationSet.PackageNormalizedVersion == package.NormalizedVersion;

            if (!matchesType || !matchesUpload || !matchesPackage)
            {
                throw new InvalidOperationException("The validation set does not match the staged symbol upload.");
            }

            var matchesPhase = SymbolPromotionValidationConfiguration.IsPromotion(validationSet);
            if (!isPromotion)
            {
                matchesPhase = validationSet.PackageValidations.All(validation => IsStagedSymbolValidatorName(validation.Type));
            }

            if (!matchesPhase)
            {
                throw new InvalidOperationException("The validation set does not match the requested staged symbol phase.");
            }
        }

        private static bool IsStagedSymbolValidator(ValidationConfigurationItem validation)
        {
            return validation.Name == ValidatorName.SymbolScan || validation.Name == ValidatorName.SymbolsValidator;
        }

        private static void EnsureValidConfiguration(IReadOnlyCollection<ValidationConfigurationItem> validations)
        {
            var hasSymbolScan = validations.Any(v => v.Name == ValidatorName.SymbolScan);
            var hasSymbolsValidator = validations.Any(v => v.Name == ValidatorName.SymbolsValidator);
            var allMustSucceed = validations.All(v => v.FailureBehavior == ValidationFailureBehavior.MustSucceed);
            var dependenciesAreSupported = validations.All(v => v.RequiredValidations.All(IsStagedSymbolValidatorName));

            if (validations.Count != 2 || !hasSymbolScan || !hasSymbolsValidator || !allMustSucceed || !dependenciesAreSupported)
            {
                throw new InvalidOperationException("Staged symbols require SymbolScan and SymbolsValidator as required validations without ingestion dependencies.");
            }
        }

        private static bool IsStagedSymbolValidatorName(string name)
        {
            return name == ValidatorName.SymbolScan || name == ValidatorName.SymbolsValidator;
        }
    }
}
