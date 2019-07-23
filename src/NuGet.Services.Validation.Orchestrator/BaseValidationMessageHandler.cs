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
    public abstract class BaseValidationMessageHandler<T> : IMessageHandler<PackageValidationMessageData> where T : class, IEntity
    {
        private readonly ValidationConfiguration _configs;
        private readonly IEntityService<T> _entityService;
        private readonly IValidationSetProvider<T> _validationSetProvider;
        private readonly IValidationSetProcessor _validationSetProcessor;
        private readonly IValidationOutcomeProcessor<T> _validationOutcomeProcessor;
        private readonly ITelemetryService _telemetryService;
        private readonly ILogger _logger;

        public BaseValidationMessageHandler(
            IOptionsSnapshot<ValidationConfiguration> validationConfigsAccessor,
            IEntityService<T> entityService,
            IValidationSetProvider<T> validationSetProvider,
            IValidationSetProcessor validationSetProcessor,
            IValidationOutcomeProcessor<T> validationOutcomeProcessor,
            ITelemetryService telemetryService,
            ILogger logger)
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
            _entityService = entityService ?? throw new ArgumentNullException(nameof(entityService));
            _validationSetProvider = validationSetProvider ?? throw new ArgumentNullException(nameof(validationSetProvider));
            _validationSetProcessor = validationSetProcessor ?? throw new ArgumentNullException(nameof(validationSetProcessor));
            _validationOutcomeProcessor = validationOutcomeProcessor ?? throw new ArgumentNullException(nameof(validationOutcomeProcessor));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected abstract ValidatingType ValidatingType { get; }
        protected abstract bool ShouldNoOpDueToDeletion { get; }

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
            IValidatingEntity<T> entity;
            using (_logger.BeginScope(
                "Finding validation set for {ValidatingType} validation ID {ValidationId}",
                ValidatingType,
                message.ValidationId))
            {
                validationSet = await _validationSetProvider.TryGetParentValidationSetAsync(message.ValidationId);
                if (validationSet == null)
                {
                    _logger.LogError("Could not find validation set for {ValidationId}.", message.ValidationId);
                    return false;
                }

                if (validationSet.ValidatingType != ValidatingType)
                {
                    _logger.LogError("Validation set {ValidationSetId} is not for a {TypeName}.", message.ValidationId, ValidatingType);
                    return false;
                }

                entity = _entityService.FindPackageByKey(validationSet.PackageKey);
                if (entity == null)
                {
                    _logger.LogError(
                        "Could not find {ValidatingType} {PackageId} {PackageVersion} {Key} for validation set {ValidationSetId}.",
                        ValidatingType,
                        validationSet.PackageId,
                        validationSet.PackageNormalizedVersion,
                        validationSet.PackageKey,
                        validationSet.ValidationTrackingId);
                    return false;
                }
                
                // Immediately halt validation of a soft deleted package.
                if (ShouldNoOpDueToDeletion && entity.Status == PackageStatus.Deleted)
                {
                    _logger.LogWarning(
                        "{ValidatingType} {PackageId} {PackageVersion} {Key} is soft deleted. Dropping message for " +
                        "validation set {ValidationSetId}.",
                        ValidatingType,
                        validationSet.PackageId,
                        validationSet.PackageNormalizedVersion,
                        entity.Key,
                        validationSet.ValidationTrackingId);
                    return true;
                }
            }

            using (_logger.BeginScope(
                "Handling check validator message for {ValidatingType} {PackageId} {PackageVersion} {Key} " +
                "validation set {ValidationSetId}",
                ValidatingType,
                validationSet.PackageId,
                validationSet.PackageNormalizedVersion,
                validationSet.PackageKey,
                validationSet.ValidationTrackingId))
            {
                await ProcessValidationSetAsync(entity, validationSet, scheduleNextCheck: false);
            }

            return true;
        }

        private async Task<bool> ProcessValidationSetAsync(ProcessValidationSetData message, int deliveryCount)
        {
            using (_logger.BeginScope(
                "Handling process validation set message for {ValidatingType} {PackageId} {PackageVersion} {Key} " +
                "{ValidationSetId}",
                ValidatingType,
                message.PackageId,
                message.PackageNormalizedVersion,
                message.EntityKey,
                message.ValidationTrackingId))
            {
                // When a message is sent from the Gallery with validation of a new entity, the EntityKey will be null
                // because the message is sent to the service bus before the entity is persisted in the DB. However
                // when a revalidation happens or when the message is re-sent by the orchestrator the message will
                // contain the key. In this case the key is used to find the entity to validate.
                var entity = message.EntityKey.HasValue
                    ? _entityService.FindPackageByKey(message.EntityKey.Value)
                    : _entityService.FindPackageByIdAndVersionStrict(message.PackageId, message.PackageNormalizedVersion);

                if (entity == null)
                {
                    // no package in DB yet. Might have received message a bit early, need to retry later
                    if (deliveryCount - 1 >= _configs.MissingPackageRetryCount)
                    {
                        _logger.LogWarning(
                            "Could not find {ValidatingType} {PackageId} {PackageVersion} {Key} in DB after " +
                            "{DeliveryCount} tries. Discarding the message.",
                            ValidatingType,
                            message.PackageId,
                            message.PackageNormalizedVersion,
                            message.EntityKey,
                            deliveryCount);

                        _telemetryService.TrackMissingPackageForValidationMessage(
                            message.PackageId,
                            message.PackageNormalizedVersion,
                            message.ValidationTrackingId.ToString());

                        return true;
                    }
                    else
                    {
                        _logger.LogInformation(
                            "Could not find {ValidatingType} {PackageId} {PackageVersion} {Key} in DB. Retrying.",
                            ValidatingType,
                            message.PackageId,
                            message.PackageNormalizedVersion,
                            message.EntityKey);

                        return false;
                    }
                }

                // Immediately halt validation of a soft deleted package.
                if (ShouldNoOpDueToDeletion && entity.Status == PackageStatus.Deleted)
                {
                    _logger.LogWarning(
                        "{ValidatingType} {PackageId} {PackageNormalizedVersion} {Key} is soft deleted. Dropping " +
                        "message for validation set {ValidationSetId}.",
                        ValidatingType,
                        message.PackageId,
                        message.PackageNormalizedVersion,
                        entity.Key,
                        message.ValidationTrackingId);

                    return true;
                }

                var validationSet = await _validationSetProvider.TryGetOrCreateValidationSetAsync(message, entity);

                if (validationSet == null)
                {
                    _logger.LogInformation(
                        "The validation request for {ValidatingType} {PackageId} {PackageVersion} {Key} " +
                        "{ValidationSetId} is a duplicate. Discarding the message.",
                        ValidatingType,
                        message.PackageId,
                        message.PackageNormalizedVersion,
                        entity.Key,
                        message.ValidationTrackingId);
                    return true;
                }

                await ProcessValidationSetAsync(entity, validationSet, scheduleNextCheck: true);
            }

            return true;
        }

        private async Task ProcessValidationSetAsync(
            IValidatingEntity<T> entity,
            PackageValidationSet validationSet,
            bool scheduleNextCheck)
        {
            if (validationSet.ValidationSetStatus == ValidationSetStatus.Completed)
            {
                _logger.LogInformation(
                    "The validation set {ValidatingType} {PackageId} {PackageVersion} {Key} {ValidationSetId} is " +
                    "already completed. Discarding the message.",
                    ValidatingType,
                    validationSet.PackageId,
                    validationSet.PackageNormalizedVersion,
                    validationSet.PackageKey,
                    validationSet.ValidationTrackingId);
                return;
            }

            var processorStats = await _validationSetProcessor.ProcessValidationsAsync(validationSet);

            await _validationOutcomeProcessor.ProcessValidationOutcomeAsync(
                validationSet,
                entity,
                processorStats,
                scheduleNextCheck);
        }
    }
}
