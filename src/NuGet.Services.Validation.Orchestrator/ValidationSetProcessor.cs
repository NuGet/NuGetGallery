// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGetGallery;

namespace NuGet.Services.Validation.Orchestrator
{
    public class ValidationSetProcessor : IValidationSetProcessor
    {
        private const int MaxProcessAttempts = 20;
        private readonly IValidatorProvider _validatorProvider;
        private readonly IValidationStorageService _validationStorageService;
        private readonly ValidationConfiguration _validationConfiguration;
        private readonly ICorePackageFileService _packageFileService;
        private readonly ILogger<ValidationSetProcessor> _logger;

        public ValidationSetProcessor(
            IValidatorProvider validatorProvider,
            IValidationStorageService validationStorageService,
            IOptionsSnapshot<ValidationConfiguration> validationConfigurationAccessor,
            ICorePackageFileService packageFileService,
            ILogger<ValidationSetProcessor> logger)
        {
            _validatorProvider = validatorProvider ?? throw new ArgumentNullException(nameof(validatorProvider));
            _validationStorageService = validationStorageService ?? throw new ArgumentNullException(nameof(validationStorageService));
            if (validationConfigurationAccessor == null)
            {
                throw new ArgumentNullException(nameof(validationConfigurationAccessor));
            }
            _validationConfiguration = validationConfigurationAccessor.Value ?? throw new ArgumentException($"The Value property cannot be null", nameof(validationConfigurationAccessor));
            _packageFileService = packageFileService ?? throw new ArgumentNullException(nameof(packageFileService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task ProcessValidationsAsync(PackageValidationSet validationSet, Package package)
        {
            _logger.LogInformation("Starting processing validation request for {PackageId} {PackageVersion}, validation set {ValidationSetId}",
                package.PackageRegistration.Id,
                package.NormalizedVersion,
                validationSet.ValidationTrackingId);
            bool tryMoreValidations = true;
            int loopLimit = MaxProcessAttempts;
            await ProcessIncompleteValidations(validationSet, package);
            do
            {
                // we will try to start more validations in case previous validation start attempts
                // result in Succeeded validation immediately (i.e. the validation was synchronous).
                // If no validation start attempts resulted in succeeded validation (ProcessNotStartedValidations
                // returns false) we move on and will check on progress later.
                // loopLimit is there to prevent looping here infinitely if there are any bugs that
                // cause ProcessNotStartedValidations to always return true.
                tryMoreValidations = await ProcessNotStartedValidations(validationSet, package);
            } while (tryMoreValidations && loopLimit-- > 0);
            if (loopLimit <= 0)
            {
                _logger.LogWarning("Too many processing attempts ({NumAttempts}) for {PackageId} {PackageVersion}, validation set {ValidationSetId}",
                    MaxProcessAttempts,
                    validationSet.PackageId,
                    validationSet.PackageNormalizedVersion,
                    validationSet.ValidationTrackingId);
            }
        }

        private async Task<bool> ProcessIncompleteValidations(PackageValidationSet validationSet, Package package)
        {
            bool tryMoreValidations = false;
            foreach (var packageValidation in validationSet.PackageValidations.Where(v => v.ValidationStatus == ValidationStatus.Incomplete))
            {
                using (_logger.BeginScope("Incomplete {ValidationType} Key {ValidationId}", packageValidation.Type, packageValidation.Key))
                {
                    _logger.LogInformation("Processing incomplete validation {ValidationType} for {PackageId} {PackageVersion}, validation set {ValidationSetId}, {ValidationId}",
                        packageValidation.Type,
                        package.PackageRegistration.Id,
                        package.NormalizedVersion,
                        validationSet.ValidationTrackingId,
                        packageValidation.Key);
                    var validationConfiguration = GetValidationConfiguration(packageValidation.Type);
                    if (validationConfiguration == null)
                    {
                        await OnUnknownValidation(packageValidation);
                        continue;
                    }

                    var validator = _validatorProvider.GetValidator(packageValidation.Type);
                    var validationRequest = await CreateValidationRequest(packageValidation.PackageValidationSet, packageValidation, package, validationConfiguration);
                    var validationStatus = await validator.GetStatusAsync(validationRequest);
                    _logger.LogInformation("New status for validation {ValidationType} for {PackageId} {PackageVersion} is {ValidationStatus}, validation set {ValidationSetId}, {ValidationId}",
                        packageValidation.Type,
                        package.PackageRegistration.Id,
                        package.NormalizedVersion,
                        validationStatus,
                        validationSet.ValidationTrackingId,
                        packageValidation.Key);

                    switch (validationStatus)
                    {
                        case ValidationStatus.Incomplete:
                            await ProcessIncompleteValidation(packageValidation, validationConfiguration);
                            break;

                        case ValidationStatus.Failed:
                            await _validationStorageService.UpdateValidationStatusAsync(packageValidation, validationStatus);
                            break;

                        case ValidationStatus.Succeeded:
                            await _validationStorageService.UpdateValidationStatusAsync(packageValidation, validationStatus);
                            // need another iteration to try running new validations
                            tryMoreValidations = true;
                            break;

                        default:
                            throw new InvalidOperationException($"Unexpected validation state: " +
                                $"DB: {ValidationStatus.Incomplete} ({(int)ValidationStatus.Incomplete}), " +
                                $"Actual: {validationStatus} {(int)validationStatus}");
                    }
                }
            }

            return tryMoreValidations;
        }

        private async Task<bool> ProcessNotStartedValidations(PackageValidationSet validationSet, Package package)
        {
            bool tryMoreValidations = false;
            foreach (var packageValidation in validationSet.PackageValidations.Where(v => v.ValidationStatus == ValidationStatus.NotStarted))
            {
                using (_logger.BeginScope("Not started {ValidationType} Key {ValidationId}", packageValidation.Type, packageValidation.Key))
                {
                    _logger.LogInformation("Processing not started validation {ValidationType} for {PackageId} {PackageVersion}, validation set {ValidationSetId}, {ValidationId}",
                    packageValidation.Type,
                    package.PackageRegistration.Id,
                    package.NormalizedVersion,
                    validationSet.ValidationTrackingId,
                    packageValidation.Key);
                    var validationConfiguration = GetValidationConfiguration(packageValidation.Type);
                    if (validationConfiguration == null)
                    {
                        await OnUnknownValidation(packageValidation);
                        continue;
                    }

                    bool prerequisitesAreMet = ArePrerequisitesMet(packageValidation, validationSet);
                    if (!prerequisitesAreMet)
                    {
                        _logger.LogInformation("Prerequisites are not met for validation {ValidationType} for {PackageId} {PackageVersion}, validation set {ValidationSetId}, {ValidationId}",
                            packageValidation.Type,
                            package.PackageRegistration.Id,
                            package.NormalizedVersion,
                            validationSet.ValidationTrackingId,
                            packageValidation.Key);
                        continue;
                    }

                    var validator = _validatorProvider.GetValidator(packageValidation.Type);
                    var validationRequest = await CreateValidationRequest(packageValidation.PackageValidationSet, packageValidation, package, validationConfiguration);
                    var validationStatus = await validator.GetStatusAsync(validationRequest);

                    if (validationStatus == ValidationStatus.NotStarted)
                    {
                        _logger.LogInformation("Requesting validation {ValidationType} for {PackageId} {PackageVersion}, validation set {ValidationSetId}, {ValidationId}, {NupkgUrl}",
                            packageValidation.Type,
                            package.PackageRegistration.Id,
                            package.NormalizedVersion,
                            validationSet.ValidationTrackingId,
                            packageValidation.Key,
                            validationRequest.NupkgUrl);
                        validationStatus = await validator.StartValidationAsync(validationRequest);
                        _logger.LogInformation("Got validationStatus = {ValidationStatus} for validation {ValidationType} for {PackageId} {PackageVersion}, validation set {ValidationSetId}, {ValidationId}",
                            validationStatus,
                            packageValidation.Type,
                            package.PackageRegistration.Id,
                            package.NormalizedVersion,
                            validationSet.ValidationTrackingId,
                            packageValidation.Key);
                    }

                    if (validationStatus == ValidationStatus.NotStarted)
                    {
                        _logger.LogWarning("Unexpected NotStarted state after start attempt for validation {ValidationName}, package: {PackageId} {PackageVersion}",
                            packageValidation.Type,
                            packageValidation.PackageValidationSet.PackageId,
                            packageValidation.PackageValidationSet.PackageNormalizedVersion);
                    }
                    else
                    {
                        await _validationStorageService.MarkValidationStartedAsync(packageValidation, validationStatus);
                    }

                    tryMoreValidations = tryMoreValidations || validationStatus == ValidationStatus.Succeeded;
                }
            }

            return tryMoreValidations;
        }

        private ValidationConfigurationItem GetValidationConfiguration(string validationName)
        {
            return _validationConfiguration.Validations
                .FirstOrDefault(v => v.Name == validationName);
        }

        private async Task OnUnknownValidation(PackageValidation packageValidation)
        {
            _logger.LogWarning("Failing validation {Validation} for package {PackageId} {PackageVersion} for which we don't have a configuration",
                packageValidation.Type,
                packageValidation.PackageValidationSet.PackageId,
                packageValidation.PackageValidationSet.PackageNormalizedVersion);

            await _validationStorageService.UpdateValidationStatusAsync(packageValidation, ValidationStatus.Failed);
        }

        private async Task ProcessIncompleteValidation(PackageValidation packageValidation, ValidationConfigurationItem validationConfiguration)
        {
            // need to check validation timeout
            var duration = DateTime.UtcNow - packageValidation.Started;
            if (duration > validationConfiguration.FailAfter)
            {
                _logger.LogWarning("Failing validation {Validation} for package {PackageId} {PackageVersion} that runs longer than configured failure timout {FailAfter}",
                    packageValidation.Type,
                    packageValidation.PackageValidationSet.PackageId,
                    packageValidation.PackageValidationSet.PackageNormalizedVersion,
                    validationConfiguration.FailAfter);
                await _validationStorageService.UpdateValidationStatusAsync(packageValidation, ValidationStatus.Failed);
                return;
            }
        }

        private async Task<IValidationRequest> CreateValidationRequest(
            PackageValidationSet packageValidationSet,
            PackageValidation packageValidation,
            Package package,
            ValidationConfigurationItem validationConfiguration)
        {
            var validationRequest = new ValidationRequest(
                validationId: packageValidation.Key,
                packageKey: packageValidationSet.PackageKey,
                packageId: packageValidationSet.PackageId,
                packageVersion: packageValidationSet.PackageNormalizedVersion,
                nupkgUrl: (await GetNupkgUrl(package, validationConfiguration)).AbsoluteUri
            );

            return validationRequest;
        }

        private async Task<Uri> GetNupkgUrl(Package package, ValidationConfigurationItem validationConfiguration)
        {
            if (await _packageFileService.DoesPackageFileExistAsync(package))
            {
                return await _packageFileService.GetPackageReadUriAsync(package);
            }
            else if (await _packageFileService.DoesValidationPackageFileExistAsync(package))
            {
                return await _packageFileService.GetValidationPackageReadUriAsync(package, DateTimeOffset.UtcNow.Add(validationConfiguration.FailAfter));
            }

            throw new Exception($"Package {package.PackageRegistration.Id} {package.NormalizedVersion} does not exist neither in validation nor in public container");
        }

        private bool ArePrerequisitesMet(PackageValidation packageValidation, PackageValidationSet packageValidationSet)
        {
            var completeValidations = new HashSet<string>(packageValidationSet
                .PackageValidations
                .Where(v => v.ValidationStatus == ValidationStatus.Succeeded)
                .Select(v => v.Type));
            var requiredValidations = _validationConfiguration
                .Validations
                .Single(v => v.Name == packageValidation.Type).RequiredValidations;

            return completeValidations.IsSupersetOf(requiredValidations);
        }
    }
}
