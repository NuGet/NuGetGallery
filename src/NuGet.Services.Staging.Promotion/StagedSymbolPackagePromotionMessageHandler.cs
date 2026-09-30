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
        private readonly IPackageValidationEnqueuer _symbolsOrchestrator;
        private readonly ILogger<StagedSymbolPackagePromotionMessageHandler> _logger;

        public StagedSymbolPackagePromotionMessageHandler(
            IEntityRepository<StagedSymbolPackage> attempts,
            IPackageValidationEnqueuer symbolsOrchestrator,
            ILogger<StagedSymbolPackagePromotionMessageHandler> logger)
        {
            _attempts = attempts ?? throw new ArgumentNullException(nameof(attempts));
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
                .SingleOrDefault(candidate => candidate.Key == message.TargetKey);
            if (attempt == null
                || attempt.Status != StagedPackageStatus.Promoting
                || attempt.ActivePromotionId != message.PromotionId
                || attempt.StagedPackageIdentity.CurrentStagedSymbolPackageKey != attempt.Key)
            {
                _logger.LogInformation("Ignoring inactive symbol promotion {PromotionId} for attempt {AttemptKey}.", message.PromotionId, message.TargetKey);
                return true;
            }

            var identity = attempt.StagedPackageIdentity;
            if (identity.StagingGroupKey.HasValue)
            {
                throw new NotSupportedException("Grouped symbol promotion is not enabled yet.");
            }

            var package = identity.Package;
            if (package.PackageStatusKey != PackageStatus.Available
                || attempt.SymbolPackage.StatusKey != PackageStatus.Staged
                || !package.PackageRegistration.Owners.Any(owner => owner.Key == identity.OwnerKey))
            {
                _logger.LogWarning("Symbol promotion {PromotionId} is no longer eligible.", message.PromotionId);
                attempt.Status = StagedPackageStatus.PromotionFailed;
                await _attempts.CommitChangesAsync();
                return true;
            }

            var trackingId = SymbolPromotionValidationTrackingId.Create(message.PromotionId, attempt.Key);
            var validation = PackageValidationMessageData.NewProcessValidationSet(package.Id, package.NormalizedVersion, trackingId, ValidatingType.StagedSymbolPackage, attempt.Key);
            await _symbolsOrchestrator.SendMessageAsync(validation);
            return true;
        }
    }
}
