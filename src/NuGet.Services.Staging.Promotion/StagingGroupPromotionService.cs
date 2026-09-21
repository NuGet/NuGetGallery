// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.Entity;
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
        private readonly IStagingGroupLockService _stagingGroupLockService;
        private readonly ILogger<StagingGroupPromotionService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="StagingGroupPromotionService"/> class.
        /// </summary>
        /// <param name="stagedPackageRepository">The staged package repository.</param>
        /// <param name="stagingGroupRepository">The staging group repository.</param>
        /// <param name="stagingGroupLockService">The staging group lock service.</param>
        /// <param name="logger">The logger.</param>
        public StagingGroupPromotionService(
            IEntityRepository<StagedPackage> stagedPackageRepository,
            IEntityRepository<StagingGroup> stagingGroupRepository,
            IStagingGroupLockService stagingGroupLockService,
            ILogger<StagingGroupPromotionService> logger)
        {
            _stagedPackageRepository = stagedPackageRepository ?? throw new ArgumentNullException(nameof(stagedPackageRepository));
            _stagingGroupRepository = stagingGroupRepository ?? throw new ArgumentNullException(nameof(stagingGroupRepository));
            _stagingGroupLockService = stagingGroupLockService ?? throw new ArgumentNullException(nameof(stagingGroupLockService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task CompletePackageAsync(StagedPackage stagedPackage)
        {
            if (stagedPackage == null)
            {
                throw new ArgumentNullException(nameof(stagedPackage));
            }

            var identity = stagedPackage.StagedPackageIdentity;
            var stagingGroup = identity?.StagingGroup;
            if (stagingGroup == null
                || identity.StagingGroupKey != stagingGroup.Key
                || !stagedPackage.ActivePromotionId.HasValue
                || stagingGroup.ActivePromotionId != stagedPackage.ActivePromotionId)
            {
                throw new ArgumentException("The staged package must belong to an active group promotion.", nameof(stagedPackage));
            }

            using (_logger.BeginScope("Staging group {StagingGroupKey}, promotion {PromotionId}", stagingGroup.Key, stagedPackage.ActivePromotionId))
            {
                await _stagingGroupLockService.AcquireAsync(stagingGroup.Key);
                _logger.LogInformation("Completing successful staged package {StagedPackageKey}.", stagedPackage.Key);
                stagedPackage.Status = StagedPackageStatus.Succeeded;
                var activeMembers = _stagedPackageRepository
                    .GetAll()
                    .Include(candidate => candidate.StagedPackageIdentity)
                    .Where(candidate => candidate.StagedPackageIdentity.StagingGroupKey == stagingGroup.Key)
                    .Where(candidate => candidate.StagedPackageIdentity.CurrentStagedPackageKey == candidate.Key)
                    .Where(candidate => candidate.ActivePromotionId == stagedPackage.ActivePromotionId)
                    .ToList();

                if (activeMembers.Count > 0 && activeMembers.All(candidate => candidate.Status == StagedPackageStatus.Succeeded))
                {
                    foreach (var activeMember in activeMembers)
                    {
                        activeMember.StagedPackageIdentity.CurrentStagedPackageKey = null;
                        activeMember.StagedPackageIdentity.CurrentStagedPackage = null;
                        activeMember.StagedPackageIdentity.StagingGroupKey = null;
                        activeMember.StagedPackageIdentity.StagingGroup = null;
                        _stagedPackageRepository.DeleteOnCommit(activeMember);
                    }

                    _stagingGroupRepository.DeleteOnCommit(stagingGroup);
                    _logger.LogInformation("Completed staging group promotion with {PackageCount} successful packages.", activeMembers.Count);
                }
                else
                {
                    var remainingPackageCount = activeMembers.Count(candidate => candidate.Status != StagedPackageStatus.Succeeded);
                    _logger.LogInformation(
                        "Retained successful staged package while {RemainingPackageCount} packages remain.",
                        remainingPackageCount);
                }
            }
        }
    }
}
