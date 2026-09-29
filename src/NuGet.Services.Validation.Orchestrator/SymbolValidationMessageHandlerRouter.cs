// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.Entities;
using NuGet.Services.ServiceBus;

namespace NuGet.Services.Validation.Orchestrator
{
    /// <summary>
    /// Routes symbol validation messages to the ordinary or staged symbol validation pipeline.
    /// </summary>
    public class SymbolValidationMessageHandlerRouter : IMessageHandler<PackageValidationMessageData>
    {
        private readonly IValidationMessageHandler<SymbolPackage> _symbolHandler;
        private readonly IValidationMessageHandler<StagedSymbolPackage> _stagedSymbolHandler;
        private readonly IValidationStorageService _validationStorageService;
        private readonly ILogger<SymbolValidationMessageHandlerRouter> _logger;

        public SymbolValidationMessageHandlerRouter(
            IValidationMessageHandler<SymbolPackage> symbolHandler,
            IValidationMessageHandler<StagedSymbolPackage> stagedSymbolHandler,
            IValidationStorageService validationStorageService,
            ILogger<SymbolValidationMessageHandlerRouter> logger)
        {
            _symbolHandler = symbolHandler ?? throw new ArgumentNullException(nameof(symbolHandler));
            _stagedSymbolHandler = stagedSymbolHandler ?? throw new ArgumentNullException(nameof(stagedSymbolHandler));
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
                case ValidatingType.SymbolPackage:
                    return await _symbolHandler.HandleAsync(message);
                case ValidatingType.StagedSymbolPackage:
                    return await _stagedSymbolHandler.HandleAsync(message);
                default:
                    throw new NotSupportedException($"The validating type '{validatingType}' is not supported by the symbol orchestrator.");
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
                    throw new NotSupportedException($"The symbol validation message type '{message.Type}' is not supported by the symbol orchestrator.");
            }
        }
    }
}
