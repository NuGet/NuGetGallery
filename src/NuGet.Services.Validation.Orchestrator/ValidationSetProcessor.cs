// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Services.Validation.Orchestrator.Telemetry;

namespace NuGet.Services.Validation.Orchestrator
{
    public class ValidationSetProcessor : IValidationSetProcessor
    {
        private const int MaxProcessAttempts = 20;
        private readonly IValidatorProvider _validatorProvider;
        private readonly IValidationStorageService _validationStorageService;
        private readonly ValidationConfiguration _validationConfiguration;
        private readonly SasDefinitionConfiguration _sasDefinitionConfiguration;
        private readonly IValidationFileService _packageFileService;
        private readonly ITelemetryService _telemetryService;
        private readonly ILogger<ValidationSetProcessor> _logger;

        public ValidationSetProcessor(
            IValidatorProvider validatorProvider,
            IValidationStorageService validationStorageService,
            IOptionsSnapshot<ValidationConfiguration> validationConfigurationAccessor,
            IOptionsSnapshot<SasDefinitionConfiguration> sasDefinitionConfigurationAccessor,
            IValidationFileService packageFileService,
            ITelemetryService telemetryService,
            ILogger<ValidationSetProcessor> logger)
        {
            _validatorProvider = validatorProvider ?? throw new ArgumentNullException(nameof(validatorProvider));
            _validationStorageService = validationStorageService ?? throw new ArgumentNullException(nameof(validationStorageService));
            if (validationConfigurationAccessor == null)
            {
                throw new ArgumentNullException(nameof(validationConfigurationAccessor));
            }
            _validationConfiguration = validationConfigurationAccessor.Value ?? throw new ArgumentException($"The Value property cannot be null", nameof(validationConfigurationAccessor));
            _sasDefinitionConfiguration = (sasDefinitionConfigurationAccessor == null || sasDefinitionConfigurationAccessor.Value == null) ? new SasDefinitionConfiguration() : sasDefinitionConfigurationAccessor.Value;
            _packageFileService = packageFileService ?? throw new ArgumentNullException(nameof(packageFileService));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<ValidationSetProcessorResult> ProcessValidationsAsync(PackageValidationSet validationSet)
        {
            _logger.LogInformation("Starting processing validation request for {PackageId} {PackageVersion}, validation set {ValidationSetId}",
                validationSet.PackageId,
                validationSet.PackageNormalizedVersion,
                validationSet.ValidationTrackingId);
            var processorStats = new ValidationSetProcessorResult();
            int loopLimit = MaxProcessAttempts;
            await ProcessIncompleteValidations(validationSet, processorStats);
            bool hadSucceededValidations = false;
            do
            {
                // we will try to start more validations in case previous validation start attempts
                // result in Succeeded validation immediately (i.e. the validation was synchronous).
                // If no validation start attempts resulted in succeeded validation (ProcessNotStartedValidations
                // returns false) we move on and will check on progress later.
                // loopLimit is there to prevent looping here infinitely if there are any bugs that
                // cause ProcessNotStartedValidations to always return true.
                hadSucceededValidations = await ProcessNotStartedValidations(validationSet, processorStats);
            } while (hadSucceededValidations && loopLimit-- > 0);
            if (loopLimit <= 0)
            {
                _logger.LogWarning("Too many processing attempts ({NumAttempts}) for {PackageId} {PackageVersion}, validation set {ValidationSetId}",
                    MaxProcessAttempts,
                    validationSet.PackageId,
                    validationSet.PackageNormalizedVersion,
                    validationSet.ValidationTrackingId);
            }

            return processorStats;
        }

        private async Task ProcessIncompleteValidations(PackageValidationSet validationSet, ValidationSetProcessorResult processorStats)
        {
            foreach (var packageValidation in validationSet.PackageValidations.Where(v => v.ValidationStatus == ValidationStatus.Incomplete))
            {
                using (_logger.BeginScope("Incomplete {ValidationType} Key {ValidationId}", packageValidation.Type, packageValidation.Key))
                {
                    _logger.LogInformation("Processing incomplete validation {ValidationType} for {PackageId} {PackageVersion}, validation set {ValidationSetId}, {ValidationId}",
                        packageValidation.Type,
                        validationSet.PackageId,
                        validationSet.PackageNormalizedVersion,
                        validationSet.ValidationTrackingId,
                        packageValidation.Key);
                    var validationConfiguration = GetValidationConfiguration(packageValidation.Type);
                    if (validationConfiguration == null)
                    {
                        await OnUnknownValidation(packageValidation);
                        continue;
                    }

                    var validator = _validatorProvider.GetNuGetValidator(packageValidation.Type);
                    var validationRequest = await CreateNuGetValidationRequest(packageValidation.PackageValidationSet, packageValidation);
                    var validationResponse = await validator.GetResponseAsync(validationRequest);

                    if (validationResponse.Status != ValidationStatus.Incomplete)
                    {
                        _logger.LogInformation(
                            "New status for validation {ValidationType} for {PackageId} {PackageVersion} is " +
                            "{ValidationStatus}, validation set {ValidationSetId}, {ValidationId}",
                           packageValidation.Type,
                           validationSet.PackageId,
                           validationSet.PackageNormalizedVersion,
                           validationResponse.Status,
                           validationSet.ValidationTrackingId,
                           packageValidation.Key);
                    }
                    else
                    {
                        _logger.LogDebug(
                            "Validation {ValidationType} for {PackageId} {PackageVersion} is already " +
                            "{ValidationStatus}, validation set {ValidationSetId}, {ValidationId}",
                            packageValidation.Type,
                            validationSet.PackageId,
                            validationSet.PackageNormalizedVersion,
                            validationResponse.Status,
                            validationSet.ValidationTrackingId,
                            packageValidation.Key);
                    }

                    switch (validationResponse.Status)
                    {
                        case ValidationStatus.Incomplete:
                            break;

                        case ValidationStatus.Failed:
                            await _validationStorageService.UpdateValidationStatusAsync(packageValidation, validationResponse);
                            await validator.CleanUpAsync(validationRequest);
                            break;

                        case ValidationStatus.Succeeded:
                            await _validationStorageService.UpdateValidationStatusAsync(packageValidation, validationResponse);
                            await validator.CleanUpAsync(validationRequest);
                            UpdateStatsForValidationSuccess(processorStats, validationConfiguration);
                            break;

                        default:
                            throw new InvalidOperationException($"Unexpected validation state: " +
                                $"DB: {ValidationStatus.Incomplete} ({(int)ValidationStatus.Incomplete}), " +
                                $"Actual: {validationResponse.Status} {(int)validationResponse.Status}");
                    }
                }
            }
        }

        private async Task<bool> ProcessNotStartedValidations(PackageValidationSet validationSet, ValidationSetProcessorResult processorStats)
        {
            bool tryMoreValidations = false;
            foreach (var packageValidation in validationSet.PackageValidations.Where(v => v.ValidationStatus == ValidationStatus.NotStarted))
            {
                using (_logger.BeginScope("Not started {ValidationType} Key {ValidationId}", packageValidation.Type, packageValidation.Key))
                {
                    _logger.LogInformation("Processing not started validation {ValidationType} for {PackageId} {PackageVersion}, validation set {ValidationSetId}, {ValidationId}",
                    packageValidation.Type,
                    validationSet.PackageId,
                    validationSet.PackageNormalizedVersion,
                    validationSet.ValidationTrackingId,
                    packageValidation.Key);
                    var validationConfiguration = GetValidationConfiguration(packageValidation.Type);
                    if (validationConfiguration == null)
                    {
                        await OnUnknownValidation(packageValidation);
                        continue;
                    }

                    if (!validationConfiguration.ShouldStart)
                    {
                        continue;
                    }

                    bool prerequisitesAreMet = ArePrerequisitesMet(packageValidation, validationSet);
                    if (!prerequisitesAreMet)
                    {
                        _logger.LogInformation("Prerequisites are not met for validation {ValidationType} for {PackageId} {PackageVersion}, validation set {ValidationSetId}, {ValidationId}",
                            packageValidation.Type,
                            validationSet.PackageId,
                            validationSet.PackageNormalizedVersion,
                            validationSet.ValidationTrackingId,
                            packageValidation.Key);
                        continue;
                    }

                    var validator = _validatorProvider.GetNuGetValidator(packageValidation.Type);
                    var validationRequest = await CreateNuGetValidationRequest(packageValidation.PackageValidationSet, packageValidation);
                    var validationResult = await validator.GetResponseAsync(validationRequest);

                    if (validationResult.Status == ValidationStatus.NotStarted)
                    {
                        _logger.LogInformation("Requesting validation {ValidationType} for {PackageId} {PackageVersion}, validation set {ValidationSetId}, {ValidationId}, {NupkgUrl}",
                            packageValidation.Type,
                            validationSet.PackageId,
                            validationSet.PackageNormalizedVersion,
                            validationSet.ValidationTrackingId,
                            packageValidation.Key,
                            validationRequest.NupkgUrl);
                        try
                        {
                            validationResult = await validator.StartAsync(validationRequest);
                        }
                        catch (Exception e) when (validationConfiguration.FailureBehavior != ValidationFailureBehavior.MustSucceed)
                        {
                            // ignore exceptions for optional validators
                            _logger.LogWarning(0, e, "Got exception while running optional validation {ValidationType} for " +
                                "{PackageId} {PackageVersion}, validation set {ValidationSetId}, {ValidationId}, {NupkgUrl}",
                                packageValidation.Type,
                                validationSet.PackageId,
                                validationSet.PackageNormalizedVersion,
                                validationSet.ValidationTrackingId,
                                packageValidation.Key,
                                validationRequest.NupkgUrl);
                        }
                        _logger.LogInformation("Got validationStatus = {ValidationStatus} for validation {ValidationType} for {PackageId} {PackageVersion}, validation set {ValidationSetId}, {ValidationId}",
                            validationResult.Status,
                            packageValidation.Type,
                            validationSet.PackageId,
                            validationSet.PackageNormalizedVersion,
                            validationSet.ValidationTrackingId,
                            packageValidation.Key);
                    }

                    if (validationResult.Status == ValidationStatus.NotStarted)
                    {
                        _logger.LogWarning("Unexpected NotStarted state after start attempt for validation {ValidationName}, package: {PackageId} {PackageVersion}",
                            packageValidation.Type,
                            packageValidation.PackageValidationSet.PackageId,
                            packageValidation.PackageValidationSet.PackageNormalizedVersion);
                    }
                    else
                    {
                        await _validationStorageService.MarkValidationStartedAsync(packageValidation, validationResult);

                        if (validationResult.Status == ValidationStatus.Succeeded
                            || validationResult.Status == ValidationStatus.Failed)
                        {
                            await validator.CleanUpAsync(validationRequest);
                        }

                        _telemetryService.TrackValidatorStarted(validationSet.PackageId, validationSet.PackageNormalizedVersion, validationSet.ValidationTrackingId, packageValidation.Type);

                        if (validationResult.Status == ValidationStatus.Succeeded)
                        {
                            UpdateStatsForValidationSuccess(processorStats, validationConfiguration);
                            tryMoreValidations = true;
                        }
                    }
                }
            }

            return tryMoreValidations;
        }

        private void UpdateStatsForValidationSuccess(ValidationSetProcessorResult processorStats, ValidationConfigurationItem validationConfiguration)
        {
            processorStats.AnyValidationSucceeded = true;
            if (validationConfiguration.FailureBehavior == ValidationFailureBehavior.MustSucceed)
            {
                processorStats.AnyRequiredValidationSucceeded = true;
            }
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

            await _validationStorageService.UpdateValidationStatusAsync(packageValidation, NuGetValidationResponse.Failed);
        }

        private async Task<INuGetValidationRequest> CreateNuGetValidationRequest(
            PackageValidationSet packageValidationSet,
            PackageValidation packageValidation)
        {
            var nupkgUrl = await _packageFileService.GetPackageForValidationSetReadUriAsync(
                packageValidationSet,
                _sasDefinitionConfiguration.ValidationSetProcessorSasDefinition,
                DateTimeOffset.UtcNow.Add(_validationConfiguration.TimeoutValidationSetAfter));

            var validationRequest = new NuGetValidationRequest(
                validationId: packageValidation.Key,
                packageKey: packageValidationSet.PackageKey.Value,
                packageId: packageValidationSet.PackageId,
                packageVersion: packageValidationSet.PackageNormalizedVersion,
                nupkgUrl: nupkgUrl.AbsoluteUri);

            return validationRequest;
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
