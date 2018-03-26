// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Services.Validation.Orchestrator.Telemetry;
using NuGetGallery;

namespace NuGet.Services.Validation.Orchestrator
{
    public class ValidationSetProvider : IValidationSetProvider
    {
        private readonly IValidationStorageService _validationStorageService;
        private readonly IValidationPackageFileService _packageFileService;
        private readonly IValidatorProvider _validatorProvider;
        private readonly ValidationConfiguration _validationConfiguration;
        private readonly ITelemetryService _telemetryService;
        private readonly ILogger<ValidationSetProvider> _logger;

        public ValidationSetProvider(
            IValidationStorageService validationStorageService,
            IValidationPackageFileService packageFileService,
            IValidatorProvider validatorProvider,
            IOptionsSnapshot<ValidationConfiguration> validationConfigurationAccessor,
            ITelemetryService telemetryService,
            ILogger<ValidationSetProvider> logger)
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

        public async Task<PackageValidationSet> TryGetOrCreateValidationSetAsync(Guid validationTrackingId, Package package)
        {
            var validationSet = await _validationStorageService.GetValidationSetAsync(validationTrackingId);

            if (validationSet == null)
            {
                var shouldSkip = await _validationStorageService.OtherRecentValidationSetForPackageExists(
                    package.Key,
                    _validationConfiguration.NewValidationRequestDeduplicationWindow,
                    validationTrackingId);
                if (shouldSkip)
                {
                    return null;
                }

                validationSet = InitializeValidationSet(validationTrackingId, package);

                if (package.PackageStatusKey == PackageStatus.Available)
                {
                    var packageETag = await _packageFileService.CopyPackageFileForValidationSetAsync(validationSet);

                    // This indicates that the package in the package container is expected to not change.
                    validationSet.PackageETag = packageETag;
                }
                else
                {
                    await _packageFileService.CopyValidationPackageForValidationSetAsync(validationSet);

                    // This indicates that the package in the packages container is expected to not exist (i.e. it has
                    // has no etag at all).
                    validationSet.PackageETag = null;
                }

                // If there are any processors in the validation set, back up the original. We back up from the
                // validation set copy to avoid concurrency issues.
                if (validationSet.PackageValidations.Any(x => _validatorProvider.IsProcessor(x.Type)))
                {
                    await _packageFileService.BackupPackageFileFromValidationSetPackageAsync(package, validationSet);
                }

                validationSet = await PersistValidationSetAsync(validationSet, package);
            }
            else
            {
                var sameId = package.PackageRegistration.Id.Equals(
                    validationSet.PackageId,
                    StringComparison.InvariantCultureIgnoreCase);

                var sameVersion = package.NormalizedVersion.Equals(
                    validationSet.PackageNormalizedVersion,
                    StringComparison.InvariantCultureIgnoreCase);

                if (!sameId || !sameVersion)
                {
                    throw new InvalidOperationException(
                        $"Validation set package identity ({validationSet.PackageId} {validationSet.PackageNormalizedVersion})" +
                        $"does not match expected package identity ({package.PackageRegistration.Id} {package.NormalizedVersion}).");
                }

                var sameKey = package.Key == validationSet.PackageKey;
                
                if (!sameKey)
                {
                    throw new InvalidOperationException($"Validation set package key ({validationSet.PackageKey}) " +
                        $"does not match expected package key ({package.Key}).");
                }
            }

            return validationSet;
        }

        private async Task<PackageValidationSet> PersistValidationSetAsync(PackageValidationSet validationSet, Package package)
        {
            _logger.LogInformation("Persisting validation set {ValidationSetId} for package {PackageId} {PackageVersion} (package key {PackageKey})",
                validationSet.ValidationTrackingId,
                package.PackageRegistration.Id,
                package.NormalizedVersion,
                package.Key);

            var persistedValidationSet = await _validationStorageService.CreateValidationSetAsync(validationSet);

            // Only track the validation set creation time when this is the first validation set to be created for that
            // package. There will be more than one validation set when an admin has requested a manual revalidation.
            // This can happen much later than when the package was created so the duration is less interesting in that
            // case.
            if (await _validationStorageService.GetValidationSetCountAsync(package.Key) == 1)
            {
                _telemetryService.TrackDurationToValidationSetCreation(validationSet.Created - package.Created);
            }

            return persistedValidationSet;
        }

        private PackageValidationSet InitializeValidationSet(Guid validationTrackingId, Package package)
        {
            _logger.LogInformation("Initializing validation set {ValidationSetId} for package {PackageId} {PackageVersion} (package key {PackageKey})",
                validationTrackingId,
                package.PackageRegistration.Id,
                package.NormalizedVersion,
                package.Key);

            var now = DateTime.UtcNow;

            var validationSet = new PackageValidationSet
            {
                Created = now,
                PackageId = package.PackageRegistration.Id,
                PackageNormalizedVersion = package.NormalizedVersion,
                PackageKey = package.Key,
                PackageValidations = new List<PackageValidation>(),
                Updated = now,
                ValidationTrackingId = validationTrackingId,
            };

            foreach (var validation in _validationConfiguration.Validations)
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
