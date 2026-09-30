// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Services.Entities;
using NuGet.Services.Staging;
using NuGet.Services.Validation.Orchestrator.Telemetry;
using NuGetGallery;

namespace NuGet.Services.Validation.Orchestrator
{
    /// <summary>
    /// Applies staged validation normally and completes ingestion through Gallery promotion logic.
    /// </summary>
    public class StagedSymbolPackageValidationOutcomeProcessor : IValidationOutcomeProcessor<StagedSymbolPackage>
    {
        private readonly ValidationOutcomeProcessor<StagedSymbolPackage> _validation;
        private readonly Lazy<IStagedSymbolPackagePromotionService> _promotion;
        private readonly IValidationStorageService _storage;
        private readonly IValidationFileService _files;
        private readonly IPackageValidationEnqueuer _enqueuer;
        private readonly ValidationConfiguration _configuration;
        private readonly ITelemetryService _telemetry;
        private readonly ILogger<StagedSymbolPackageValidationOutcomeProcessor> _logger;

        public StagedSymbolPackageValidationOutcomeProcessor(
            ValidationOutcomeProcessor<StagedSymbolPackage> validation,
            Lazy<IStagedSymbolPackagePromotionService> promotion,
            IValidationStorageService storage,
            IValidationFileService files,
            IPackageValidationEnqueuer enqueuer,
            IOptionsSnapshot<ValidationConfiguration> configuration,
            ITelemetryService telemetry,
            ILogger<StagedSymbolPackageValidationOutcomeProcessor> logger)
        {
            _validation = validation ?? throw new ArgumentNullException(nameof(validation));
            _promotion = promotion ?? throw new ArgumentNullException(nameof(promotion));
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _files = files ?? throw new ArgumentNullException(nameof(files));
            _enqueuer = enqueuer ?? throw new ArgumentNullException(nameof(enqueuer));
            _configuration = configuration?.Value ?? throw new ArgumentException("Validation configuration is required.", nameof(configuration));
            _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task ProcessValidationOutcomeAsync(
            PackageValidationSet validationSet,
            IValidatingEntity<StagedSymbolPackage> validatingEntity,
            ValidationSetProcessorResult currentCallStats,
            bool scheduleNextCheck)
        {
            if (validationSet == null)
            {
                throw new ArgumentNullException(nameof(validationSet));
            }

            if (validatingEntity == null)
            {
                throw new ArgumentNullException(nameof(validatingEntity));
            }

            if (currentCallStats == null)
            {
                throw new ArgumentNullException(nameof(currentCallStats));
            }

            if (!SymbolPromotionValidationConfiguration.IsPromotion(validationSet))
            {
                await _validation.ProcessValidationOutcomeAsync(validationSet, validatingEntity, currentCallStats, scheduleNextCheck);
                return;
            }

            var attempt = validatingEntity.EntityRecord;
            if (!MatchesPromotion(validationSet, attempt))
            {
                _logger.LogInformation("Ignoring stale symbol promotion outcome {ValidationTrackingId}.", validationSet.ValidationTrackingId);
                return;
            }

            var promotionId = attempt.ActivePromotionId.Value;
            if (validationSet.ValidationSetStatus == ValidationSetStatus.Completed)
            {
                await CleanUpAsync(validationSet, attempt.Key, promotionId);
                return;
            }

            if (!_configuration.EnableStagedSymbolPromotion)
            {
                throw new NotSupportedException("Staged symbol promotion is not enabled.");
            }

            var ingestion = validationSet.PackageValidations.Single();
            if (ingestion.ValidationStatus == ValidationStatus.NotStarted || ingestion.ValidationStatus == ValidationStatus.Incomplete)
            {
                if (DateTime.UtcNow - validationSet.Created <= _configuration.TimeoutValidationSetAfter)
                {
                    await _storage.UpdateValidationSetAsync(validationSet);
                    if (scheduleNextCheck)
                    {
                        var message = PackageValidationMessageData.NewProcessValidationSet(validationSet.PackageId, validationSet.PackageNormalizedVersion,
                            validationSet.ValidationTrackingId, ValidatingType.StagedSymbolPackage, attempt.Key);
                        await _enqueuer.SendMessageAsync(message, DateTimeOffset.UtcNow + _configuration.ValidationMessageRecheckPeriod);
                    }

                    return;
                }

                _logger.LogWarning("Symbol promotion ingestion {ValidationTrackingId} timed out.", validationSet.ValidationTrackingId);
                _telemetry.TrackValidationSetTimeout(validationSet.PackageId, validationSet.PackageNormalizedVersion, validationSet.ValidationTrackingId);
                await _storage.UpdateValidationStatusAsync(ingestion, NuGetValidationResponse.Failed);
                await _promotion.Value.FailAsync(attempt.Key, promotionId);
            }
            else if (ingestion.ValidationStatus == ValidationStatus.Succeeded)
            {
                await _promotion.Value.CompleteAsync(attempt.Key, promotionId);
            }
            else if (ingestion.ValidationStatus == ValidationStatus.Failed)
            {
                await _promotion.Value.FailAsync(attempt.Key, promotionId);
            }
            else
            {
                throw new InvalidOperationException("The symbol ingestion outcome is not supported.");
            }

            validationSet.ValidationSetStatus = ValidationSetStatus.Completed;
            await _storage.UpdateValidationSetAsync(validationSet);
            _telemetry.TrackTotalValidationDuration(validationSet.PackageId, validationSet.PackageNormalizedVersion,
                validationSet.ValidationTrackingId, DateTime.UtcNow - validationSet.Created, attempt.Status == StagedPackageStatus.Succeeded);
            await CleanUpAsync(validationSet, attempt.Key, promotionId);
        }

        private async Task CleanUpAsync(PackageValidationSet validationSet, int attemptKey, Guid promotionId)
        {
            await _files.DeletePackageForValidationSetAsync(validationSet);
            await _promotion.Value.CleanUpAsync(attemptKey, promotionId);
        }

        private static bool MatchesPromotion(PackageValidationSet validationSet, StagedSymbolPackage attempt)
        {
            var matchesAttempt = validationSet.PackageKey == attempt.Key && validationSet.PackageETag == attempt.UploadedBlobETag;
            var isCurrentAttempt = attempt.StagedPackageIdentity.CurrentStagedSymbolPackageKey == attempt.Key;
            var hasPromotion = attempt.ActivePromotionId.HasValue;
            var isPromotionState =
                attempt.Status == StagedPackageStatus.Promoting ||
                attempt.Status == StagedPackageStatus.Succeeded ||
                attempt.Status == StagedPackageStatus.PromotionFailed;

            if (!matchesAttempt || !isCurrentAttempt || !hasPromotion || !isPromotionState)
            {
                return false;
            }

            return validationSet.ValidationTrackingId == SymbolPromotionValidationTrackingId.Create(attempt.ActivePromotionId.Value, attempt.Key);
        }
    }
}
