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
    /// <summary>
    /// The message handler for Symbols.
    /// </summary>
    public class SymbolValidationMessageHandler : IMessageHandler<PackageValidationMessageData>
    {
        private readonly ValidationConfiguration _configs;
        private readonly IEntityService<SymbolPackage> _gallerySymbolService;
        private readonly IValidationSetProvider<SymbolPackage> _validationSetProvider;
        private readonly IValidationSetProcessor _validationSetProcessor;
        private readonly IValidationOutcomeProcessor<SymbolPackage> _validationOutcomeProcessor;
        private readonly ITelemetryService _telemetryService;
        private readonly ILogger<SymbolValidationMessageHandler> _logger;

        public SymbolValidationMessageHandler(
            IOptionsSnapshot<ValidationConfiguration> validationConfigsAccessor,
            IEntityService<SymbolPackage> gallerySymbolService,
            IValidationSetProvider<SymbolPackage> validationSetProvider,
            IValidationSetProcessor validationSetProcessor,
            IValidationOutcomeProcessor<SymbolPackage> validationOutcomeProcessor,
            ITelemetryService telemetryService,
            ILogger<SymbolValidationMessageHandler> logger)
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
            _gallerySymbolService = gallerySymbolService ?? throw new ArgumentNullException(nameof(gallerySymbolService));
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

            using (_logger.BeginScope("Handling symbol message for {PackageId} {PackageVersion} validation set {ValidationSetId}",
                message.PackageId,
                message.PackageNormalizedVersion,
                message.ValidationTrackingId))
            {
                // When a message is sent from the Gallery with validation of a new entity, the EntityKey will be null because the message is sent to the service bus before the entity is persisted in the DB
                // However when a revalidation happens or when the message is re-sent by the orchestrator the message will contain the key. In this case the key is used to find the entity to validate.
                var symbolPackageEntity = message.EntityKey.HasValue
                    ? _gallerySymbolService.FindPackageByKey(message.EntityKey.Value)
                    : _gallerySymbolService.FindPackageByIdAndVersionStrict(message.PackageId, message.PackageNormalizedVersion);

                if (symbolPackageEntity == null)
                {
                    // no package in DB yet. Might have received message a bit early, need to retry later
                    if (message.DeliveryCount - 1 >= _configs.MissingPackageRetryCount)
                    {
                        _logger.LogWarning("Could not find symbols for package {PackageId} {PackageNormalizedVersion} in DB after {DeliveryCount} tries, dropping message",
                            message.PackageId,
                            message.PackageNormalizedVersion,
                            message.DeliveryCount);

                        _telemetryService.TrackMissingPackageForValidationMessage(
                            message.PackageId,
                            message.PackageNormalizedVersion,
                            message.ValidationTrackingId.ToString());

                        return true;
                    }
                    else
                    {
                        _logger.LogInformation("Could not find symbols for package {PackageId} {PackageNormalizedVersion} {Key} in DB, retrying",
                            message.PackageId,
                            message.PackageNormalizedVersion,
                            message.EntityKey.HasValue);

                        return false;
                    }
                }

                var validationSet = await _validationSetProvider.TryGetOrCreateValidationSetAsync(message, symbolPackageEntity);

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

                if (validationSet.ValidationSetStatus == ValidationSetStatus.Completed)
                {
                    _logger.LogInformation(
                        "The validation set {PackageId} {PackageNormalizedVersion} {ValidationSetId} is already " +
                        "completed. Discarding the message.",
                        message.PackageId,
                        message.PackageNormalizedVersion,
                        message.ValidationTrackingId);
                    return true;
                }

                var processorStats = await _validationSetProcessor.ProcessValidationsAsync(validationSet);
                // As part of the processing the validation outcome the orchestrator will send itself a message if validation are still being processed.
                await _validationOutcomeProcessor.ProcessValidationOutcomeAsync(validationSet, symbolPackageEntity, processorStats);
            }

            return true;
        }
    }
}
