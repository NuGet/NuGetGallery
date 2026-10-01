// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.Entities;
using NuGet.Services.Staging;
using NuGet.Services.Validation;
using NuGetGallery;

namespace NuGet.Services.Staging.Promotion
{
    /// <summary>
    /// Dispatches accepted symbol promotions to ingestion-only symbol orchestration.
    /// </summary>
    public class StagedSymbolPackagePromotionMessageHandler : IStagingPromotionMessageHandler<StagedSymbolPackage>
    {
        private readonly IEntityRepository<StagedSymbolPackage> _attempts;
        private readonly IStagingGroupPromotionService _groups;
        private readonly IPackageValidationEnqueuer _symbolsOrchestrator;
        private readonly ILogger<StagedSymbolPackagePromotionMessageHandler> _logger;

        public StagedSymbolPackagePromotionMessageHandler(
            IEntityRepository<StagedSymbolPackage> attempts,
            IStagingGroupPromotionService groups,
            IPackageValidationEnqueuer symbolsOrchestrator,
            ILogger<StagedSymbolPackagePromotionMessageHandler> logger)
        {
            _attempts = attempts ?? throw new ArgumentNullException(nameof(attempts));
            _groups = groups ?? throw new ArgumentNullException(nameof(groups));
            _symbolsOrchestrator = symbolsOrchestrator ?? throw new ArgumentNullException(nameof(symbolsOrchestrator));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<bool> HandleAsync(StagingPromotionMessage message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            if (message.TargetType != StagingPromotionTargetType.StagedSymbolPackage)
            {
                throw new ArgumentException("The message must identify a staged symbol promotion.", nameof(message));
            }

            var attempt = _attempts.GetAll().Include(candidate => candidate.SymbolPackage)
                .Include(candidate => candidate.StagedPackageIdentity.Package.PackageRegistration.Owners)
                .Include(candidate => candidate.StagedPackageIdentity.StagingGroup)
                .SingleOrDefault(candidate => candidate.Key == message.TargetKey);
            if (attempt == null
                || attempt.ActivePromotionId != message.PromotionId
                || attempt.StagedPackageIdentity.CurrentStagedSymbolPackageKey != attempt.Key)
            {
                _logger.LogInformation("Ignoring inactive symbol promotion {PromotionId} for attempt {AttemptKey}.", message.PromotionId, message.TargetKey);
                return true;
            }

            var identity = attempt.StagedPackageIdentity;
            if (identity.StagingGroupKey.HasValue && identity.StagingGroup?.ActivePromotionId != message.PromotionId)
            {
                _logger.LogInformation("Ignoring inactive group symbol promotion {PromotionId}.", message.PromotionId);
                return true;
            }

            if (attempt.Status != StagedPackageStatus.Promoting)
            {
                var isTerminal = attempt.Status == StagedPackageStatus.Succeeded || attempt.Status == StagedPackageStatus.PromotionFailed;
                if (identity.StagingGroupKey.HasValue && isTerminal)
                {
                    _logger.LogInformation("Resuming staging group finalization for an already completed symbol.");
                    await _groups.TryFinalizeAsync(identity.StagingGroupKey.Value, message.PromotionId);
                }
                else
                {
                    _logger.LogInformation("Ignoring inactive symbol promotion {PromotionId} for attempt {AttemptKey}.", message.PromotionId, message.TargetKey);
                }

                return true;
            }

            var package = identity.Package;
            var isPublishedGroupSymbol = identity.StagingGroupKey.HasValue && attempt.SymbolPackage.StatusKey == PackageStatus.Available;
            var isEligible = package.PackageStatusKey == PackageStatus.Available && attempt.SymbolPackage.StatusKey == PackageStatus.Staged;
            if (isEligible)
            {
                isEligible = package.PackageRegistration.Owners.Any(owner => owner.Key == identity.OwnerKey);
            }

            if (!isPublishedGroupSymbol && !isEligible)
            {
                _logger.LogWarning("Symbol promotion {PromotionId} is no longer eligible.", message.PromotionId);
                attempt.Status = StagedPackageStatus.PromotionFailed;
                await _attempts.CommitChangesAsync();
                if (identity.StagingGroupKey.HasValue)
                {
                    await _groups.TryFinalizeAsync(identity.StagingGroupKey.Value, message.PromotionId);
                }

                return true;
            }

            var trackingId = SymbolPromotionValidationTrackingId.Create(message.PromotionId, attempt.Key);
            var validation = PackageValidationMessageData.NewProcessValidationSet(package.Id, package.NormalizedVersion, trackingId, ValidatingType.StagedSymbolPackage, attempt.Key);
            await _symbolsOrchestrator.SendMessageAsync(validation);
            return true;
        }
    }
}
