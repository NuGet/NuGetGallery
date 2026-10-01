// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    /// <inheritdoc />
    public class StagingGroupPromotionService : IStagingGroupPromotionService
    {
        private readonly IEntityRepository<StagedPackage> _stagedPackageRepository;
        private readonly IEntityRepository<StagedSymbolPackage> _stagedSymbolPackageRepository;
        private readonly IEntityRepository<StagedPackageIdentity> _stagedPackageIdentityRepository;
        private readonly IEntityRepository<StagingGroup> _stagingGroupRepository;
        private readonly ILogger<StagingGroupPromotionService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="StagingGroupPromotionService"/> class.
        /// </summary>
        /// <param name="stagedPackageRepository">The staged package repository.</param>
        /// <param name="stagedSymbolPackageRepository">The staged symbol repository.</param>
        /// <param name="stagedPackageIdentityRepository">The staging identity repository.</param>
        /// <param name="stagingGroupRepository">The staging group repository.</param>
        /// <param name="logger">The logger.</param>
        public StagingGroupPromotionService(
            IEntityRepository<StagedPackage> stagedPackageRepository,
            IEntityRepository<StagedSymbolPackage> stagedSymbolPackageRepository,
            IEntityRepository<StagedPackageIdentity> stagedPackageIdentityRepository,
            IEntityRepository<StagingGroup> stagingGroupRepository,
            ILogger<StagingGroupPromotionService> logger)
        {
            _stagedPackageRepository = stagedPackageRepository ?? throw new ArgumentNullException(nameof(stagedPackageRepository));
            _stagedSymbolPackageRepository = stagedSymbolPackageRepository ?? throw new ArgumentNullException(nameof(stagedSymbolPackageRepository));
            _stagedPackageIdentityRepository = stagedPackageIdentityRepository ?? throw new ArgumentNullException(nameof(stagedPackageIdentityRepository));
            _stagingGroupRepository = stagingGroupRepository ?? throw new ArgumentNullException(nameof(stagingGroupRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public void MarkPackageSucceeded(StagedPackage stagedPackage)
        {
            if (stagedPackage == null)
            {
                throw new ArgumentNullException(nameof(stagedPackage));
            }

            var identity = stagedPackage.StagedPackageIdentity;
            var stagingGroup = identity?.StagingGroup;
            if (stagingGroup == null
                || identity.StagingGroupKey != stagingGroup.Key
                || stagedPackage.Status != StagedPackageStatus.Promoting
                || !stagedPackage.ActivePromotionId.HasValue
                || stagingGroup.ActivePromotionId != stagedPackage.ActivePromotionId)
            {
                throw new ArgumentException("The staged package must belong to an active group promotion.", nameof(stagedPackage));
            }

            using (_logger.BeginScope("Staging group {StagingGroupKey}, promotion {PromotionId}", stagingGroup.Key, stagedPackage.ActivePromotionId))
            {
                _logger.LogInformation("Marking staged package {StagedPackageKey} as successful.", stagedPackage.Key);
                stagedPackage.Status = StagedPackageStatus.Succeeded;
            }
        }

        /// <inheritdoc />
        public async Task TryFinalizeAsync(int stagingGroupKey, Guid promotionId)
        {
            if (stagingGroupKey <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(stagingGroupKey));
            }

            if (promotionId == Guid.Empty)
            {
                throw new ArgumentOutOfRangeException(nameof(promotionId));
            }

            using (_logger.BeginScope("Staging group {StagingGroupKey}, promotion {PromotionId}", stagingGroupKey, promotionId))
            {
                try
                {
                    await _stagedPackageRepository.ExecuteInTransactionAsync(async () =>
                    {
                        var stagingGroup = _stagingGroupRepository
                            .GetAll()
                            .SingleOrDefault(candidate => candidate.Key == stagingGroupKey);
                        if (stagingGroup == null || stagingGroup.ActivePromotionId != promotionId)
                        {
                            _logger.LogInformation("Ignoring inactive staging group finalization attempt.");
                            return;
                        }

                        var activeMembers = _stagedPackageRepository
                            .GetAll()
                            .Include(candidate => candidate.StagedPackageIdentity)
                            .Where(candidate => candidate.StagedPackageIdentity.StagingGroupKey == stagingGroupKey)
                            .Where(candidate => candidate.StagedPackageIdentity.CurrentStagedPackageKey == candidate.Key)
                            .Where(candidate => candidate.ActivePromotionId == promotionId)
                            .ToList();

                        var activeSymbols = _stagedSymbolPackageRepository.GetAll()
                            .Include(candidate => candidate.StagedPackageIdentity)
                            .Where(candidate => candidate.StagedPackageIdentity.StagingGroupKey == stagingGroupKey)
                            .Where(candidate => candidate.StagedPackageIdentity.CurrentStagedSymbolPackageKey == candidate.Key)
                            .Where(candidate => candidate.ActivePromotionId == promotionId)
                            .ToList();

                        var remainingCount = activeMembers.Count(candidate => !IsTerminal(candidate.Status));
                        remainingCount += activeSymbols.Count(candidate => !IsTerminal(candidate.Status));
                        if (activeMembers.Count + activeSymbols.Count == 0 || remainingCount > 0)
                        {
                            _logger.LogInformation("Group finalization is not ready while {RemainingCount} artifacts remain.", remainingCount);
                            return;
                        }

                        // EF clears deleted attempts' navigation properties, so capture their identities first.
                        var activeIdentities = activeMembers.Select(candidate => candidate.StagedPackageIdentity)
                            .Concat(activeSymbols.Select(candidate => candidate.StagedPackageIdentity))
                            .Distinct()
                            .ToList();

                        foreach (var activeMember in activeMembers)
                        {
                            if (activeMember.Status == StagedPackageStatus.Succeeded)
                            {
                                var identity = activeMember.StagedPackageIdentity;
                                identity.CurrentStagedPackageKey = null;
                                identity.CurrentStagedPackage = null;
                                _stagedPackageRepository.DeleteOnCommit(activeMember);
                            }
                            else
                            {
                                activeMember.ActivePromotionId = null;
                            }
                        }

                        foreach (var symbols in activeSymbols.Where(candidate => candidate.Status == StagedPackageStatus.Succeeded))
                        {
                            var identity = symbols.StagedPackageIdentity;
                            identity.CurrentStagedSymbolPackageKey = null;
                            identity.CurrentStagedSymbolPackage = null;
                            _stagedSymbolPackageRepository.DeleteOnCommit(symbols);
                        }

                        var completedIdentities = activeIdentities
                            .Where(identity => !identity.CurrentStagedPackageKey.HasValue && !identity.CurrentStagedSymbolPackageKey.HasValue)
                            .ToList();
                        foreach (var identity in completedIdentities)
                        {
                            identity.StagingGroupKey = null;
                            identity.StagingGroup = null;
                        }

                        stagingGroup.ActivePromotionId = null;
                        stagingGroup.PromotionMessageSentDate = null;
                        await _stagedPackageRepository.CommitChangesAsync();

                        if (completedIdentities.Count > 0)
                        {
                            // Clear the current-attempt relationships before deleting their identities.
                            foreach (var identity in completedIdentities)
                            {
                                _stagedPackageIdentityRepository.DeleteOnCommit(identity);
                            }

                            await _stagedPackageRepository.CommitChangesAsync();
                        }

                        var succeededCount = activeMembers.Count(candidate => candidate.Status == StagedPackageStatus.Succeeded);
                        succeededCount += activeSymbols.Count(candidate => candidate.Status == StagedPackageStatus.Succeeded);
                        var failedCount = activeMembers.Count(candidate => candidate.Status == StagedPackageStatus.PromotionFailed);
                        failedCount += activeSymbols.Count(candidate => candidate.Status == StagedPackageStatus.PromotionFailed);
                        _logger.LogInformation(
                            "Completed staging group promotion with {SucceededCount} successful and {FailedCount} failed artifacts; retained the group.",
                            succeededCount,
                            failedCount);
                    });
                }
                catch (DbUpdateConcurrencyException)
                {
                    var stagingGroup = _stagingGroupRepository
                        .GetAll()
                        .AsNoTracking()
                        .SingleOrDefault(candidate => candidate.Key == stagingGroupKey);
                    if (stagingGroup == null || stagingGroup.ActivePromotionId != promotionId)
                    {
                        _logger.LogInformation("Another handler completed the staging group promotion.");
                        return;
                    }

                    throw;
                }
            }
        }

        private static bool IsTerminal(StagedPackageStatus status)
        {
            return status == StagedPackageStatus.Succeeded || status == StagedPackageStatus.PromotionFailed;
        }
    }
}
