// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.Entities;
using NuGetGallery;

namespace NuGet.Services.Staging.Promotion
{
    /// <summary>
    /// Coordinates successful package completion within an active staging group promotion.
    /// </summary>
    public class StagingGroupPromotionService : IStagingGroupPromotionService
    {
        private readonly IEntityRepository<StagedPackage> _stagedPackageRepository;
        private readonly IEntityRepository<StagingGroup> _stagingGroupRepository;
        private readonly ILogger<StagingGroupPromotionService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="StagingGroupPromotionService"/> class.
        /// </summary>
        /// <param name="stagedPackageRepository">The staged package repository.</param>
        /// <param name="stagingGroupRepository">The staging group repository.</param>
        /// <param name="logger">The logger.</param>
        public StagingGroupPromotionService(
            IEntityRepository<StagedPackage> stagedPackageRepository,
            IEntityRepository<StagingGroup> stagingGroupRepository,
            ILogger<StagingGroupPromotionService> logger)
        {
            _stagedPackageRepository = stagedPackageRepository ?? throw new ArgumentNullException(nameof(stagedPackageRepository));
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

                        var remainingPackageCount = activeMembers.Count(candidate => candidate.Status != StagedPackageStatus.Succeeded);
                        if (activeMembers.Count == 0 || remainingPackageCount > 0)
                        {
                            _logger.LogInformation("Group finalization is not ready while {RemainingPackageCount} packages remain.", remainingPackageCount);
                            return;
                        }

                        foreach (var activeMember in activeMembers)
                        {
                            activeMember.StagedPackageIdentity.CurrentStagedPackageKey = null;
                            activeMember.StagedPackageIdentity.CurrentStagedPackage = null;
                            activeMember.StagedPackageIdentity.StagingGroupKey = null;
                            activeMember.StagedPackageIdentity.StagingGroup = null;
                            _stagedPackageRepository.DeleteOnCommit(activeMember);
                        }

                        stagingGroup.ActivePromotionId = null;
                        await _stagedPackageRepository.CommitChangesAsync();
                        _logger.LogInformation("Completed staging group promotion with {PackageCount} successful packages and retained the empty group.", activeMembers.Count);
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
    }
}
