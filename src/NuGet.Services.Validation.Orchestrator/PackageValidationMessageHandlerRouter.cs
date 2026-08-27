// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.Entities;
using NuGet.Services.ServiceBus;

namespace NuGet.Services.Validation.Orchestrator
{
    public class PackageValidationMessageHandlerRouter : IMessageHandler<PackageValidationMessageData>
    {
        private readonly PackageValidationMessageHandler _packageHandler;
        private readonly StagedPackageValidationMessageHandler _stagedPackageHandler;
        private readonly IValidationStorageService _validationStorageService;
        private readonly ILogger<PackageValidationMessageHandlerRouter> _logger;

        public PackageValidationMessageHandlerRouter(
            PackageValidationMessageHandler packageHandler,
            StagedPackageValidationMessageHandler stagedPackageHandler,
            IValidationStorageService validationStorageService,
            ILogger<PackageValidationMessageHandlerRouter> logger)
        {
            _packageHandler = packageHandler ?? throw new ArgumentNullException(nameof(packageHandler));
            _stagedPackageHandler = stagedPackageHandler ?? throw new ArgumentNullException(nameof(stagedPackageHandler));
            _validationStorageService = validationStorageService ?? throw new ArgumentNullException(nameof(validationStorageService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<bool> HandleAsync(PackageValidationMessageData message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            var validatingType = await GetValidatingTypeAsync(message);
            if (!validatingType.HasValue)
            {
                return false;
            }

            switch (validatingType)
            {
                case ValidatingType.Package:
                    return await _packageHandler.HandleAsync(message);
                case ValidatingType.StagedPackage:
                    return await _stagedPackageHandler.HandleAsync(message);
                default:
                    throw new NotSupportedException($"The validating type '{validatingType}' is not supported by the package orchestrator.");
            }
        }

        private async Task<ValidatingType?> GetValidatingTypeAsync(PackageValidationMessageData message)
        {
            switch (message.Type)
            {
                case PackageValidationMessageType.ProcessValidationSet:
                    return message.ProcessValidationSet.ValidatingType;
                case PackageValidationMessageType.CheckValidator:
                    var parentValidationSet = await _validationStorageService.TryGetParentValidationSetAsync(message.CheckValidator.ValidationId);
                    if (parentValidationSet == null)
                    {
                        _logger.LogError("Could not find validation set for validation {ValidationId}.", message.CheckValidator.ValidationId);
                        return null;
                    }

                    return parentValidationSet.ValidatingType;
                case PackageValidationMessageType.FailValidationSet:
                    var validationSet = await _validationStorageService.GetValidationSetAsync(message.FailValidationSet.ValidationTrackingId);
                    if (validationSet == null)
                    {
                        _logger.LogError("Could not find validation set for {ValidationTrackingId}.", message.FailValidationSet.ValidationTrackingId);
                        return null;
                    }

                    return validationSet.ValidatingType;
                default:
                    throw new NotSupportedException($"The package validation message type '{message.Type}' is not supported by the package orchestrator.");
            }
        }
    }
}
