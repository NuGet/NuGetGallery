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
    public class ValidationMessageHandler : IMessageHandler<PackageValidationMessageData>
    {
        private readonly ValidationConfiguration _configs;
        private readonly ICorePackageService _galleryPackageService;
        private readonly IValidationSetProvider _validationSetProvider;
        private readonly IValidationSetProcessor _validationSetProcessor;
        private readonly IValidationOutcomeProcessor _validationOutcomeProcessor;
        private readonly ITelemetryService _telemetryService;
        private readonly ILogger<ValidationMessageHandler> _logger;

        public ValidationMessageHandler(
            IOptionsSnapshot<ValidationConfiguration> validationConfigsAccessor,
            ICorePackageService galleryPackageService,
            IValidationSetProvider validationSetProvider,
            IValidationSetProcessor validationSetProcessor,
            IValidationOutcomeProcessor validationOutcomeProcessor,
            ITelemetryService telemetryService,
            ILogger<ValidationMessageHandler> logger)
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

            using (_logger.BeginScope("Handling message for {PackageId} {PackageVersion} validation set {ValidationSetId}",
                message.PackageId,
                message.PackageVersion,
                message.ValidationTrackingId))
            {
                var package = _galleryPackageService.FindPackageByIdAndVersionStrict(message.PackageId, message.PackageVersion);

                if (package == null)
                {
                    // no package in DB yet. Might have received message a bit early, need to retry later
                    if (message.DeliveryCount - 1 >= _configs.MissingPackageRetryCount)
                    {
                        _logger.LogWarning("Could not find package {PackageId} {PackageVersion} in DB after {DeliveryCount} tries, dropping message",
                            message.PackageId,
                            message.PackageVersion,
                            message.DeliveryCount);

                        _telemetryService.TrackMissingPackageForValidationMessage(
                            message.PackageId,
                            message.PackageVersion,
                            message.ValidationTrackingId.ToString());

                        return true;
                    }
                    else
                    {
                        _logger.LogInformation("Could not find package {PackageId} {PackageVersion} in DB, retrying",
                            message.PackageId,
                            message.PackageVersion);

                        return false;
                    }
                }

                var validationSet = await _validationSetProvider.TryGetOrCreateValidationSetAsync(message.ValidationTrackingId, package);

                if (validationSet == null)
                {
                    _logger.LogInformation("The validation request for {PackageId} {PackageVersion} validation set {ValidationSetId} is a duplicate. Discarding.",
                        message.PackageId,
                        message.PackageVersion,
                        message.ValidationTrackingId);
                    return true;
                }

                await _validationSetProcessor.ProcessValidationsAsync(validationSet, package);
                await _validationOutcomeProcessor.ProcessValidationOutcomeAsync(validationSet, package);
            }
            return true;
        }
    }
}
