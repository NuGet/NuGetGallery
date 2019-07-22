// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Services.Entities;
using NuGet.Services.ServiceBus;
using NuGet.Services.Validation.Orchestrator.Telemetry;

namespace NuGet.Services.Validation.Orchestrator
{
    public class PackageValidationMessageHandler : IMessageHandler<PackageValidationMessageData>
    {
        private readonly ValidationConfiguration _configs;
        private readonly IEntityService<Package> _galleryPackageService;
        private readonly IValidationSetProvider<Package> _validationSetProvider;
        private readonly IValidationSetProcessor _validationSetProcessor;
        private readonly IValidationOutcomeProcessor<Package> _validationOutcomeProcessor;
        private readonly ITelemetryService _telemetryService;
        private readonly ILogger<PackageValidationMessageHandler> _logger;

        public PackageValidationMessageHandler(
            IOptionsSnapshot<ValidationConfiguration> validationConfigsAccessor,
            IEntityService<Package> galleryPackageService,
            IValidationSetProvider<Package> validationSetProvider,
            IValidationSetProcessor validationSetProcessor,
            IValidationOutcomeProcessor<Package> validationOutcomeProcessor,
            ITelemetryService telemetryService,
            ILogger<PackageValidationMessageHandler> logger)
        {
            if (validationConfigsAccessor == null)
            {
                throw new ArgumentNullException(nameof(validationConfigsAccessor));
            }

            if (validationConfigsAccessor.Value == null)
            {
                throw new ArgumentException(
                    $"The {nameof(IOptionsSnapshot<ValidationConfiguration>)}.{nameof(IOptionsSnapshot<ValidationConfiguration>.Value)} property cannot be null",
                    nameof(validationConfigsAccessor));
            }

            if (validationConfigsAccessor.Value.MissingPackageRetryCount < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(validationConfigsAccessor),
                    $"{nameof(ValidationConfiguration)}.{nameof(ValidationConfiguration.MissingPackageRetryCount)} must be at least 1");
            }

            _configs = validationConfigsAccessor.Value;
            _galleryPackageService = galleryPackageService ?? throw new ArgumentNullException(nameof(galleryPackageService));
            _validationSetProvider = validationSetProvider ?? throw new ArgumentNullException(nameof(validationSetProvider));
            _validationSetProcessor = validationSetProcessor ?? throw new ArgumentNullException(nameof(validationSetProcessor));
            _validationOutcomeProcessor = validationOutcomeProcessor ?? throw new ArgumentNullException(nameof(validationOutcomeProcessor));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<bool> HandleAsync(PackageValidationMessageData message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            switch (message.Type)
            {
                case PackageValidationMessageType.CheckValidator:
                    return await CheckValidatorAsync(message.CheckValidator);
                case PackageValidationMessageType.ProcessValidationSet:
                    return await ProcessValidationSetAsync(message.ProcessValidationSet, message.DeliveryCount);
                default:
                    throw new NotSupportedException($"The package validation message type '{message.Type}' is not supported.");
            }
        }

        private async Task<bool> CheckValidatorAsync(CheckValidatorData message)
        {
            PackageValidationSet validationSet;
            IValidatingEntity<Package> package;
            using (_logger.BeginScope("Finding validation set and package for validation ID {ValidationId}", message.ValidationId))
            {
                validationSet = await _validationSetProvider.TryGetParentValidationSetAsync(message.ValidationId);
                if (validationSet == null)
                {
                    _logger.LogError("Could not find validation set for {ValidationId}.", message.ValidationId);
                    return false;
                }

                if (validationSet.ValidatingType != ValidatingType.Package)
                {
                    _logger.LogError("Validation set {ValidationSetId} is not for a package.", message.ValidationId);
                    return false;
                }

                package = _galleryPackageService.FindPackageByKey(validationSet.PackageKey);
                if (package == null)
                {
                    _logger.LogError(
                        "Could not find package {PackageId} {PackageVersion} for validation set {ValidationSetId}.",
                        validationSet.PackageId,
                        validationSet.PackageNormalizedVersion,
                        validationSet.ValidationTrackingId);
                    return false;
                }
                
                // Immediately halt validation of a soft deleted package.
                if (package.Status == PackageStatus.Deleted)
                {
                    _logger.LogWarning(
                        "Package {PackageId} {PackageNormalizedVersion} (package key {PackageKey}) is soft deleted. Dropping message for validation set {ValidationSetId}.",
                        validationSet.PackageId,
                        validationSet.PackageNormalizedVersion,
                        package.Key,
                        validationSet.ValidationTrackingId);

                    return true;
                }
            }

            using (_logger.BeginScope("Handling check validator message for {PackageId} {PackageVersion} validation set {ValidationSetId}",
                validationSet.PackageId,
                validationSet.PackageNormalizedVersion,
                validationSet.ValidationTrackingId))
            {
                await ProcessValidationSetAsync(package, validationSet, scheduleNextCheck: false);
            }

            return true;
        }

        private async Task<bool> ProcessValidationSetAsync(ProcessValidationSetData message, int deliveryCount)
        {
            using (_logger.BeginScope("Handling message for {PackageId} {PackageVersion} validation set {ValidationSetId}",
                message.PackageId,
                message.PackageNormalizedVersion,
                message.ValidationTrackingId))
            {
                var package = _galleryPackageService.FindPackageByIdAndVersionStrict(message.PackageId, message.PackageNormalizedVersion);

                if (package == null)
                {
                    // no package in DB yet. Might have received message a bit early, need to retry later
                    if (deliveryCount - 1 >= _configs.MissingPackageRetryCount)
                    {
                        _logger.LogWarning("Could not find package {PackageId} {PackageNormalizedVersion} in DB after {DeliveryCount} tries, dropping message",
                            message.PackageId,
                            message.PackageNormalizedVersion,
                            deliveryCount);

                        _telemetryService.TrackMissingPackageForValidationMessage(
                            message.PackageId,
                            message.PackageNormalizedVersion,
                            message.ValidationTrackingId.ToString());

                        return true;
                    }
                    else
                    {
                        _logger.LogInformation("Could not find package {PackageId} {PackageNormalizedVersion} in DB, retrying",
                            message.PackageId,
                            message.PackageNormalizedVersion);

                        return false;
                    }
                }

                // Immediately halt validation of a soft deleted package.
                if (package.Status == PackageStatus.Deleted)
                {
                    _logger.LogWarning(
                        "Package {PackageId} {PackageNormalizedVersion} (package key {PackageKey}) is soft deleted. Dropping message for validation set {ValidationSetId}.",
                        message.PackageId,
                        message.PackageNormalizedVersion,
                        package.Key,
                        message.ValidationTrackingId);

                    return true;
                }

                var validationSet = await _validationSetProvider.TryGetOrCreateValidationSetAsync(message, package);

                if (validationSet == null)
                {
                    _logger.LogInformation(
                        "The validation request for {PackageId} {PackageNormalizedVersion} validation set " +
                        "{ValidationSetId} is a duplicate. Discarding the message.",
                        message.PackageId,
                        message.PackageNormalizedVersion,
                        message.ValidationTrackingId);
                    return true;
                }

                await ProcessValidationSetAsync(package, validationSet, scheduleNextCheck: true);
            }

            return true;
        }

        private async Task ProcessValidationSetAsync(
            IValidatingEntity<Package> package,
            PackageValidationSet validationSet,
            bool scheduleNextCheck)
        {
            if (validationSet.ValidationSetStatus == ValidationSetStatus.Completed)
            {
                _logger.LogInformation(
                    "The validation set {PackageId} {PackageNormalizedVersion} {ValidationSetId} is already " +
                    "completed. Discarding the message.",
                    validationSet.PackageId,
                    validationSet.PackageNormalizedVersion,
                    validationSet.ValidationTrackingId);
                return;
            }

            var processorStats = await _validationSetProcessor.ProcessValidationsAsync(validationSet);

            await _validationOutcomeProcessor.ProcessValidationOutcomeAsync(
                validationSet,
                package,
                processorStats,
                scheduleNextCheck);
        }
    }
}
