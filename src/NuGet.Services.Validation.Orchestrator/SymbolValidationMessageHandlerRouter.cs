// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.Entities;
using NuGet.Services.ServiceBus;
using NuGet.Services.Staging;

namespace NuGet.Services.Validation.Orchestrator
{
    /// <summary>
    /// Routes ordinary symbol validation, staged validation, and staged promotion to their own handlers.
    /// </summary>
    public class SymbolValidationMessageHandlerRouter : IMessageHandler<PackageValidationMessageData>
    {
        private readonly IValidationMessageHandler<SymbolPackage> _symbolHandler;
        private readonly IValidationMessageHandler<StagedSymbolPackage> _stagedSymbolHandler;
        private readonly IStagedSymbolPackagePromotionValidationMessageHandler _promotionHandler;
        private readonly IEntityService<StagedSymbolPackage> _stagedSymbols;
        private readonly IValidationStorageService _validationStorageService;
        private readonly ILogger<SymbolValidationMessageHandlerRouter> _logger;

        public SymbolValidationMessageHandlerRouter(
            IValidationMessageHandler<SymbolPackage> symbolHandler,
            IValidationMessageHandler<StagedSymbolPackage> stagedSymbolHandler,
            IStagedSymbolPackagePromotionValidationMessageHandler promotionHandler,
            IEntityService<StagedSymbolPackage> stagedSymbols,
            IValidationStorageService validationStorageService,
            ILogger<SymbolValidationMessageHandlerRouter> logger)
        {
            _symbolHandler = symbolHandler ?? throw new ArgumentNullException(nameof(symbolHandler));
            _stagedSymbolHandler = stagedSymbolHandler ?? throw new ArgumentNullException(nameof(stagedSymbolHandler));
            _promotionHandler = promotionHandler ?? throw new ArgumentNullException(nameof(promotionHandler));
            _stagedSymbols = stagedSymbols ?? throw new ArgumentNullException(nameof(stagedSymbols));
            _validationStorageService = validationStorageService ?? throw new ArgumentNullException(nameof(validationStorageService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<bool> HandleAsync(PackageValidationMessageData message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            var handler = await GetHandlerAsync(message);
            if (handler == null)
            {
                return false;
            }

            return await handler.HandleAsync(message);
        }

        private async Task<IMessageHandler<PackageValidationMessageData>> GetHandlerAsync(PackageValidationMessageData message)
        {
            PackageValidationSet validationSet = null;
            ValidatingType validatingType;
            switch (message.Type)
            {
                case PackageValidationMessageType.ProcessValidationSet:
                    var data = message.ProcessValidationSet;
                    validatingType = data.ValidatingType;
                    if (validatingType == ValidatingType.StagedSymbolPackage)
                    {
                        validationSet = await _validationStorageService.GetValidationSetAsync(data.ValidationTrackingId);
                        if (validationSet == null && data.EntityKey.HasValue)
                        {
                            var attempt = _stagedSymbols.FindPackageByKey(data.EntityKey.Value)?.EntityRecord;
                            if (attempt?.ActivePromotionId.HasValue == true
                                && data.ValidationTrackingId == SymbolPromotionValidationTrackingId.Create(attempt.ActivePromotionId.Value, attempt.Key))
                            {
                                return _promotionHandler;
                            }
                        }
                    }

                    break;
                case PackageValidationMessageType.CheckValidator:
                    validationSet = await _validationStorageService.TryGetParentValidationSetAsync(message.CheckValidator.ValidationId);
                    if (validationSet == null)
                    {
                        _logger.LogError("Could not find validation set for validation {ValidationId}.", message.CheckValidator.ValidationId);
                        return null;
                    }

                    validatingType = validationSet.ValidatingType;
                    break;
                case PackageValidationMessageType.FailValidationSet:
                    validationSet = await _validationStorageService.GetValidationSetAsync(message.FailValidationSet.ValidationTrackingId);
                    if (validationSet == null)
                    {
                        _logger.LogError("Could not find validation set for {ValidationTrackingId}.", message.FailValidationSet.ValidationTrackingId);
                        return null;
                    }

                    validatingType = validationSet.ValidatingType;
                    break;
                default:
                    throw new NotSupportedException($"The symbol validation message type '{message.Type}' is not supported by the symbol orchestrator.");
            }

            switch (validatingType)
            {
                case ValidatingType.SymbolPackage:
                    return _symbolHandler;
                case ValidatingType.StagedSymbolPackage:
                    if (validationSet != null && SymbolPromotionValidationConfiguration.IsPromotion(validationSet))
                    {
                        return _promotionHandler;
                    }

                    return _stagedSymbolHandler;
                default:
                    throw new NotSupportedException($"The validating type '{validatingType}' is not supported by the symbol orchestrator.");
            }
        }
    }
}
