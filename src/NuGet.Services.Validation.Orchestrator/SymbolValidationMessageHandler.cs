// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Services.ServiceBus;
using NuGet.Services.Validation.Orchestrator.Telemetry;
using NuGetGallery;

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
                var symbolPackageEntity = _gallerySymbolService.FindPackageByIdAndVersionStrict(message.PackageId, message.PackageNormalizedVersion);

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
                        _logger.LogInformation("Could not find symbols for package {PackageId} {PackageNormalizedVersion} in DB, retrying",
                            message.PackageId,
                            message.PackageNormalizedVersion);

                        return false;
                    }
                }

                var validationSet = await _validationSetProvider.TryGetOrCreateValidationSetAsync(message, symbolPackageEntity);

                if (validationSet == null)
                {
                    _logger.LogInformation("The validation request for {PackageId} {PackageNormalizedVersion} validation set {ValidationSetId} is a duplicate. Discarding.",
                        message.PackageId,
                        message.PackageNormalizedVersion,
                        message.ValidationTrackingId);
                    return true;
                }

                var processorStats = await _validationSetProcessor.ProcessValidationsAsync(validationSet);
                await _validationOutcomeProcessor.ProcessValidationOutcomeAsync(validationSet, symbolPackageEntity, processorStats);
            }

            return true;
        }
    }
}
