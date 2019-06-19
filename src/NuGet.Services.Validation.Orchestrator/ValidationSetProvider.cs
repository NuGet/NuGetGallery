// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Services.Entities;
using NuGet.Services.Validation.Orchestrator.Telemetry;

namespace NuGet.Services.Validation.Orchestrator
{
    public class ValidationSetProvider<T> : IValidationSetProvider<T> where T : class, IEntity
    {
        private readonly IValidationStorageService _validationStorageService;
        private readonly IValidationFileService _packageFileService;
        private readonly IValidatorProvider _validatorProvider;
        private readonly ValidationConfiguration _validationConfiguration;
        private readonly ITelemetryService _telemetryService;
        private readonly ILogger<ValidationSetProvider<T>> _logger;

        public ValidationSetProvider(
            IValidationStorageService validationStorageService,
            IValidationFileService packageFileService,
            IValidatorProvider validatorProvider,
            IOptionsSnapshot<ValidationConfiguration> validationConfigurationAccessor,
            ITelemetryService telemetryService,
            ILogger<ValidationSetProvider<T>> logger)
        {
            _validationStorageService = validationStorageService ?? throw new ArgumentNullException(nameof(validationStorageService));
            _packageFileService = packageFileService ?? throw new ArgumentNullException(nameof(packageFileService));
            _validatorProvider = validatorProvider ?? throw new ArgumentNullException(nameof(validatorProvider));
            if (validationConfigurationAccessor == null)
            {
                throw new ArgumentNullException(nameof(validationConfigurationAccessor));
            }
            _validationConfiguration = validationConfigurationAccessor.Value ?? throw new ArgumentException($"The Value property cannot be null", nameof(validationConfigurationAccessor));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<PackageValidationSet> TryGetOrCreateValidationSetAsync(PackageValidationMessageData message, IValidatingEntity<T> validatingEntity)
        {
            var validationSet = await _validationStorageService.GetValidationSetAsync(message.ValidationTrackingId);

            if (validationSet == null)
            {
                var shouldSkip = await _validationStorageService.OtherRecentValidationSetForPackageExists(
                    validatingEntity,
                    _validationConfiguration.NewValidationRequestDeduplicationWindow,
                    message.ValidationTrackingId);
                if (shouldSkip)
                {
                    return null;
                }

                validationSet = InitializeValidationSet(message, validatingEntity);

                if (validatingEntity.Status == PackageStatus.Available)
                {
                    var packageETag = await _packageFileService.CopyPackageFileForValidationSetAsync(validationSet);

                    // This indicates that the package in the package container is expected to not change.
                    validationSet.PackageETag = packageETag;
                }
                else
                {
                    await _packageFileService.CopyValidationPackageForValidationSetAsync(validationSet);

                    // A symbols package for the same id and version can be re-submitted. 
                    // When this happens a new validation is submitted. After validation the new symbols package will overwrite the old symbols package. 
                    // Because of this when a new validation for a symbols package is received it can already exist a symbols package in the public symbols container.
                    if (validatingEntity.ValidatingType == ValidatingType.SymbolPackage)
                    {
                        validationSet.PackageETag = await _packageFileService.GetPublicPackageBlobETagOrNullAsync(validationSet);
                    }
                    else
                    {
                        // This indicates that the package in the packages container is expected to not exist (i.e. it has
                        // has no etag at all).
                        validationSet.PackageETag = null;
                    }
                }

                // If there are any processors in the validation set, back up the original. We back up from the
                // validation set copy to avoid concurrency issues.
                if (validationSet.PackageValidations.Any(x => _validatorProvider.IsProcessor(x.Type)))
                {
                    await _packageFileService.BackupPackageFileFromValidationSetPackageAsync(validationSet);
                }

                validationSet = await PersistValidationSetAsync(validationSet, validatingEntity);
            }
            else
            {
                var sameKey = validatingEntity.Key == validationSet.PackageKey;
                
                if (!sameKey)
                {
                    throw new InvalidOperationException($"Validation set  key ({validationSet.PackageKey}) " +
                        $"does not match expected {validatingEntity.EntityRecord.GetType().Name} key ({validatingEntity.Key}).");
                }
            }

            return validationSet;
        }

        private async Task<PackageValidationSet> PersistValidationSetAsync(PackageValidationSet validationSet, IValidatingEntity<T> validatingEntity)
        {
            _logger.LogInformation("Persisting validation set {ValidationSetId} for package {PackageId} {PackageVersion} (package key {PackageKey})",
                validationSet.ValidationTrackingId,
                validationSet.PackageId,
                validationSet.PackageNormalizedVersion,
                validatingEntity.Key);

            var persistedValidationSet = await _validationStorageService.CreateValidationSetAsync(validationSet);

            // Only track the validation set creation time when this is the first validation set to be created for that
            // package. There will be more than one validation set when an admin has requested a manual revalidation.
            // This can happen much later than when the package was created so the duration is less interesting in that
            // case.
            if (await _validationStorageService.GetValidationSetCountAsync(validatingEntity) == 1)
            {
                _telemetryService.TrackDurationToValidationSetCreation(validationSet.PackageId, validationSet.PackageNormalizedVersion, validationSet.ValidationTrackingId, validationSet.Created - validatingEntity.Created);
            }

            return persistedValidationSet;
        }

        private PackageValidationSet InitializeValidationSet(PackageValidationMessageData message, IValidatingEntity<T> validatingEntity)
        {
            // If message would have the package Key the package will not need to be passed as an argument
            _logger.LogInformation("Initializing validation set {ValidationSetId} for package {PackageId} {PackageVersion} (package key {PackageKey})",
                message.ValidationTrackingId,
                message.PackageId,
                message.PackageVersion,
                validatingEntity.Key);

            var now = DateTime.UtcNow;

            var validationSet = new PackageValidationSet
            {
                Created = now,
                PackageId = message.PackageId,
                PackageNormalizedVersion = message.PackageNormalizedVersion,
                PackageKey = validatingEntity.Key,
                PackageValidations = new List<PackageValidation>(),
                Updated = now,
                ValidationTrackingId = message.ValidationTrackingId,
                ValidatingType = message.ValidatingType,
                ValidationSetStatus = ValidationSetStatus.InProgress,
            };

            var validationsToStart = _validationConfiguration
                .Validations
                .Where(v => v.ShouldStart);

            foreach (var validation in validationsToStart)
            {
                var packageValidation = new PackageValidation
                {
                    PackageValidationSet = validationSet,
                    ValidationStatus = ValidationStatus.NotStarted,
                    Type = validation.Name,
                    ValidationStatusTimestamp = now,
                };

                validationSet.PackageValidations.Add(packageValidation);
            }

            return validationSet;
        }
    }
}
