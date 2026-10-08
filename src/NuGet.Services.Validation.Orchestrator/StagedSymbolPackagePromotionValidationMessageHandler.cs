// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Jobs.Validation;
using NuGet.Jobs.Validation.Leases;
using NuGet.Services.Entities;
using NuGet.Services.Validation.Orchestrator.Telemetry;

namespace NuGet.Services.Validation.Orchestrator
{
    /// <summary>
    /// Runs ingestion and retries promotion completion without changing ordinary validation handlers.
    /// </summary>
    public class StagedSymbolPackagePromotionValidationMessageHandler : IStagedSymbolPackagePromotionValidationMessageHandler
    {
        private static readonly TimeSpan LeaseTime = TimeSpan.FromMinutes(1);

        private readonly ValidationConfiguration _configuration;
        private readonly IEntityService<StagedSymbolPackage> _entities;
        private readonly IValidationSetProvider<StagedSymbolPackage> _sets;
        private readonly IValidationSetProcessor _processor;
        private readonly StagedSymbolPackageValidationOutcomeProcessor _outcome;
        private readonly IValidationStorageService _storage;
        private readonly ILeaseService _leases;
        private readonly IPackageValidationEnqueuer _enqueuer;
        private readonly IFeatureFlagService _features;
        private readonly ITelemetryService _telemetry;
        private readonly ILogger<StagedSymbolPackagePromotionValidationMessageHandler> _logger;

        public StagedSymbolPackagePromotionValidationMessageHandler(
            IOptionsSnapshot<ValidationConfiguration> configuration,
            IEntityService<StagedSymbolPackage> entities,
            IValidationSetProvider<StagedSymbolPackage> sets,
            IValidationSetProcessor processor,
            StagedSymbolPackageValidationOutcomeProcessor outcome,
            IValidationStorageService storage,
            ILeaseService leases,
            IPackageValidationEnqueuer enqueuer,
            IFeatureFlagService features,
            ITelemetryService telemetry,
            ILogger<StagedSymbolPackagePromotionValidationMessageHandler> logger)
        {
            _configuration = configuration?.Value ?? throw new ArgumentException("Validation configuration is required.", nameof(configuration));
            if (_configuration.MissingPackageRetryCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(configuration), "MissingPackageRetryCount must be at least 1.");
            }

            _entities = entities ?? throw new ArgumentNullException(nameof(entities));
            _sets = sets ?? throw new ArgumentNullException(nameof(sets));
            _processor = processor ?? throw new ArgumentNullException(nameof(processor));
            _outcome = outcome ?? throw new ArgumentNullException(nameof(outcome));
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _leases = leases ?? throw new ArgumentNullException(nameof(leases));
            _enqueuer = enqueuer ?? throw new ArgumentNullException(nameof(enqueuer));
            _features = features ?? throw new ArgumentNullException(nameof(features));
            _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<bool> HandleAsync(PackageValidationMessageData message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            if (message.Type == PackageValidationMessageType.ProcessValidationSet)
            {
                return await ProcessAsync(message);
            }

            if (message.Type == PackageValidationMessageType.CheckValidator && !_features.IsQueueBackEnabled())
            {
                _logger.LogInformation("Ignoring disabled symbol promotion callback for validation {ValidationId}.", message.CheckValidator.ValidationId);
                return true;
            }

            PackageValidationSet set;
            switch (message.Type)
            {
                case PackageValidationMessageType.CheckValidator:
                    set = await _sets.TryGetParentValidationSetAsync(message.CheckValidator.ValidationId);
                    break;
                case PackageValidationMessageType.FailValidationSet:
                    set = await _storage.GetValidationSetAsync(message.FailValidationSet.ValidationTrackingId);
                    break;
                default:
                    throw new NotSupportedException($"The symbol promotion message type '{message.Type}' is not supported.");
            }

            if (set == null)
            {
                _logger.LogError("Could not find a validation set for symbol promotion message {MessageType}.", message.Type);
                return false;
            }

            if (!SymbolPromotionValidationConfiguration.IsPromotion(set))
            {
                throw new InvalidOperationException("The message does not identify a symbol promotion ingestion set.");
            }

            var entity = _entities.FindPackageByKey(set.PackageKey.Value);
            if (entity == null)
            {
                _logger.LogInformation("Symbol promotion attempt {AttemptKey} no longer exists; dropping its callback.", set.PackageKey);
                return true;
            }

            return await WithLeaseAsync(message, set.PackageId, set.PackageNormalizedVersion, () => ProcessSetAsync(message, set, entity));
        }

        private async Task<bool> ProcessAsync(PackageValidationMessageData message)
        {
            var data = message.ProcessValidationSet;
            if (data.ValidatingType != ValidatingType.StagedSymbolPackage || !data.EntityKey.HasValue)
            {
                throw new ArgumentException("Symbol promotion requires a staged attempt key.", nameof(message));
            }

            var entity = _entities.FindPackageByKey(data.EntityKey.Value);
            if (entity == null)
            {
                _logger.LogWarning("Could not find symbol promotion attempt {AttemptKey} for validation set {ValidationTrackingId}.", data.EntityKey, data.ValidationTrackingId);
                if (message.DeliveryCount - 1 < _configuration.MissingPackageRetryCount)
                {
                    return false;
                }

                _telemetry.TrackMissingPackageForValidationMessage(data.PackageId, data.PackageNormalizedVersion, data.ValidationTrackingId.ToString());
                return true;
            }

            return await WithLeaseAsync(message, data.PackageId, data.PackageNormalizedVersion, async () =>
            {
                var set = await _sets.TryGetOrCreateValidationSetAsync(data, entity);
                if (set == null)
                {
                    _logger.LogInformation("Ignoring inactive symbol promotion request {ValidationTrackingId}.", data.ValidationTrackingId);
                    return true;
                }

                return await ProcessSetAsync(message, set, entity);
            });
        }

        private async Task<bool> ProcessSetAsync(PackageValidationMessageData message, PackageValidationSet set, IValidatingEntity<StagedSymbolPackage> entity)
        {
            if (!SymbolPromotionValidationConfiguration.MatchesAttempt(set, entity.EntityRecord))
            {
                _logger.LogInformation("Ignoring stale symbol promotion message for validation set {ValidationTrackingId}.", set.ValidationTrackingId);
                return true;
            }

            var attempt = entity.EntityRecord;
            var isGrouped = attempt.StagedPackageIdentity.StagingGroupKey.HasValue;
            var isPublishedGroupSymbol = isGrouped && attempt.SymbolPackage.StatusKey == PackageStatus.Available;
            var ingestionStatus = set.PackageValidations.Single().ValidationStatus;
            var hasTerminalGroupIngestion = false;
            if (isGrouped)
            {
                hasTerminalGroupIngestion = ingestionStatus == ValidationStatus.Succeeded || ingestionStatus == ValidationStatus.Failed;
            }

            var isCompleting = set.ValidationSetStatus == ValidationSetStatus.Completed || attempt.Status != StagedPackageStatus.Promoting;
            if (isCompleting || isPublishedGroupSymbol || hasTerminalGroupIngestion)
            {
                await _outcome.ProcessValidationOutcomeAsync(set, entity, new ValidationSetProcessorResult(), scheduleNextCheck: false);
                return true;
            }

            if (!_configuration.EnableStagedSymbolPromotion)
            {
                throw new NotSupportedException("Staged symbol promotion is not enabled.");
            }

            var rejectBeforeIngestion = false;
            if (isGrouped && ingestionStatus == ValidationStatus.NotStarted)
            {
                rejectBeforeIngestion = !CanStartIngestion(attempt);
                if (rejectBeforeIngestion)
                {
                    _logger.LogWarning("Grouped symbol promotion {PromotionId} lost eligibility before ingestion started.", attempt.ActivePromotionId);
                }
            }

            ValidationSetProcessorResult result;
            if (message.Type == PackageValidationMessageType.FailValidationSet || rejectBeforeIngestion)
            {
                result = await _processor.ForceFailValidationSetAsync(set);
            }
            else
            {
                result = await _processor.ProcessValidationsAsync(set);
            }

            await _outcome.ProcessValidationOutcomeAsync(set, entity, result,
                scheduleNextCheck: message.Type == PackageValidationMessageType.ProcessValidationSet);
            return true;
        }

        private static bool CanStartIngestion(StagedSymbolPackage attempt)
        {
            var identity = attempt.StagedPackageIdentity;
            var package = identity.Package;
            if (package.PackageStatusKey != PackageStatus.Available || attempt.SymbolPackage.StatusKey != PackageStatus.Staged)
            {
                return false;
            }

            return package.PackageRegistration.Owners.Any(owner => owner.Key == identity.OwnerKey);
        }

        private async Task<bool> WithLeaseAsync(PackageValidationMessageData message, string id, string version, Func<Task<bool>> process)
        {
            if (!_features.IsOrchestratorLeaseEnabled())
            {
                return await process();
            }

            var name = $"{ValidatingType.StagedSymbolPackage}/{id.ToLowerInvariant()}/{version.ToLowerInvariant()}";
            var lease = await _leases.TryAcquireAsync(name, LeaseTime, CancellationToken.None);
            if (!lease.IsSuccess)
            {
                _logger.LogInformation("Symbol promotion lease {ResourceName} is unavailable.", name);
                if (message.Type != PackageValidationMessageType.CheckValidator)
                {
                    await _enqueuer.SendMessageAsync(message, DateTimeOffset.UtcNow + LeaseTime);
                }

                return true;
            }

            try
            {
                return await process();
            }
            finally
            {
                try
                {
                    if (!await _leases.ReleaseAsync(name, lease.LeaseId, CancellationToken.None))
                    {
                        _logger.LogWarning("Symbol promotion lease {ResourceName} was not released gracefully.", name);
                    }
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Failed to release symbol promotion lease {ResourceName}.", name);
                }
            }
        }
    }
}
