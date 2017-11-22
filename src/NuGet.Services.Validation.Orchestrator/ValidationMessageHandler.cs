// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.ServiceBus;
using NuGetGallery;

namespace NuGet.Services.Validation.Orchestrator
{
    public class ValidationMessageHandler : IMessageHandler<PackageValidationMessageData>
    {
        private readonly ICorePackageService _galleryPackageService;
        private readonly IValidationSetProvider _validationSetProvider;
        private readonly IValidationSetProcessor _validationSetProcessor;
        private readonly IValidationOutcomeProcessor _validationOutcomeProcessor;
        private readonly ILogger<ValidationMessageHandler> _logger;

        public ValidationMessageHandler(
            ICorePackageService galleryPackageService,
            IValidationSetProvider validationSetProvider,
            IValidationSetProcessor validationSetProcessor,
            IValidationOutcomeProcessor validationOutcomeProcessor,
            ILogger<ValidationMessageHandler> logger)
        {
            _galleryPackageService = galleryPackageService ?? throw new ArgumentNullException(nameof(galleryPackageService));
            _validationSetProvider = validationSetProvider ?? throw new ArgumentNullException(nameof(validationSetProvider));
            _validationSetProcessor = validationSetProcessor ?? throw new ArgumentNullException(nameof(validationSetProcessor));
            _validationOutcomeProcessor = validationOutcomeProcessor ?? throw new ArgumentNullException(nameof(validationOutcomeProcessor));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<bool> HandleAsync(PackageValidationMessageData message)
        {
            var package = _galleryPackageService.FindPackageByIdAndVersionStrict(message.PackageId, message.PackageVersion);

            if (package == null)
            {
                // no package in DB yet. Might have received message a bit early, need to retry later
                _logger.LogInformation("Did not find information in DB for package {PackageId} {PackageVersion}",
                    message.PackageId,
                    message.PackageVersion);
                return false;
            }

            using (_logger.BeginScope("Handling message for {PackageId} {PackageVersion} validation set {ValidationSetId}", message.PackageId, message.PackageVersion, message.ValidationTrackingId))
            {
                var validationSet = await _validationSetProvider.GetOrCreateValidationSetAsync(message.ValidationTrackingId, package);

                await _validationSetProcessor.ProcessValidationsAsync(validationSet, package);
                await _validationOutcomeProcessor.ProcessValidationOutcomeAsync(validationSet, package);
            }
            return true;
        }
    }
}
