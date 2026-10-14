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
using NuGet.Services.Validation.Orchestrator.Telemetry;
using NuGetGallery;

namespace NuGet.Services.Validation.Orchestrator
{
    /// <summary>
    /// Creates validation sets from the current immutable staged symbol upload.
    /// </summary>
    public class StagedSymbolPackageValidationSetProvider : ValidationSetProvider<StagedSymbolPackage>
    {
        private readonly IValidationFileService _fileService;
        private readonly IStagingBlobService _stagingBlobService;

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
            if (!CanValidateAttempt(message, attempt))
            {
                return null;
            }

            var validationSet = await base.TryGetOrCreateValidationSetAsync(message, validatingEntity);
            if (validationSet == null)
            {
                return null;
            }

            EnsureMatchesAttempt(validationSet, attempt);
            return validationSet;
        }

        protected override IEnumerable<ValidationConfigurationItem> GetValidationsToStart()
        {
            var validations = base.GetValidationsToStart()
                .Where(IsStagedSymbolValidator)
                .ToList();

            EnsureValidConfiguration(validations);

            return validations;
        }

        protected override async Task CopyPackageFileToValidationSetAsync(PackageValidationSet validationSet, IValidatingEntity<StagedSymbolPackage> validatingEntity)
        {
            var attempt = validatingEntity.EntityRecord;
            var uri = await _stagingBlobService.GetPackageReadUriAsync(attempt.UploadedBlobPath, attempt.UploadedBlobETag);
            await _fileService.CopyPackageUrlForValidationSetAsync(validationSet, uri.AbsoluteUri);
            validationSet.PackageETag = attempt.UploadedBlobETag;
        }

        private static bool CanValidateAttempt(ProcessValidationSetData message, StagedSymbolPackage attempt)
        {
            var package = attempt.StagedPackageIdentity.Package;
            var matchesMessage =
                message.ValidatingType == ValidatingType.StagedSymbolPackage &&
                message.EntityKey == attempt.Key &&
                string.Equals(message.PackageId, package.PackageRegistration.Id, StringComparison.OrdinalIgnoreCase) &&
                message.PackageNormalizedVersion == package.NormalizedVersion;
            var isCurrentAttempt =
                attempt.Status == StagedPackageStatus.Validating &&
                attempt.StagedPackageIdentity.CurrentStagedSymbolPackageKey == attempt.Key;
            var parentIsAvailable = package.PackageStatusKey == PackageStatus.Available;

            return matchesMessage && isCurrentAttempt && parentIsAvailable;
        }

        private static void EnsureMatchesAttempt(PackageValidationSet validationSet, StagedSymbolPackage attempt)
        {
            if (validationSet.ValidatingType != ValidatingType.StagedSymbolPackage || validationSet.PackageETag != attempt.UploadedBlobETag)
            {
                throw new InvalidOperationException("The validation set does not match the staged symbol upload.");
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
