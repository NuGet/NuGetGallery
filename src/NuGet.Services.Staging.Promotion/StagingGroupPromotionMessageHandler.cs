// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.Entities;
using NuGet.Services.Staging;
using NuGetGallery;

namespace NuGet.Services.Staging.Promotion
{
    /// <summary>
    /// Fans out an active staging group promotion to its current package attempts.
    /// </summary>
    public class StagingGroupPromotionMessageHandler : IStagingPromotionMessageHandler<StagingGroup>
    {
        private readonly IEntityRepository<StagingGroup> _stagingGroupRepository;
        private readonly IEntityRepository<StagedPackage> _stagedPackageRepository;
        private readonly IStagingPromotionMessageEnqueuer _messageEnqueuer;
        private readonly ILogger<StagingGroupPromotionMessageHandler> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="StagingGroupPromotionMessageHandler"/> class.
        /// </summary>
        /// <param name="stagingGroupRepository">The staging group repository.</param>
        /// <param name="stagedPackageRepository">The staged package repository.</param>
        /// <param name="messageEnqueuer">The staging promotion message enqueuer.</param>
        /// <param name="logger">The logger.</param>
        public StagingGroupPromotionMessageHandler(
            IEntityRepository<StagingGroup> stagingGroupRepository,
            IEntityRepository<StagedPackage> stagedPackageRepository,
            IStagingPromotionMessageEnqueuer messageEnqueuer,
            ILogger<StagingGroupPromotionMessageHandler> logger)
        {
            _stagingGroupRepository = stagingGroupRepository ?? throw new ArgumentNullException(nameof(stagingGroupRepository));
            _stagedPackageRepository = stagedPackageRepository ?? throw new ArgumentNullException(nameof(stagedPackageRepository));
            _messageEnqueuer = messageEnqueuer ?? throw new ArgumentNullException(nameof(messageEnqueuer));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<bool> HandleAsync(StagingPromotionMessage message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            if (message.TargetType != StagingPromotionTargetType.StagingGroup)
            {
                throw new ArgumentException("The promotion message must identify a staging group.", nameof(message));
            }

            using (_logger.BeginScope("Staging group {StagingGroupKey}, promotion {PromotionId}", message.TargetKey, message.PromotionId))
            {
                var group = _stagingGroupRepository
                    .GetAll()
                    .SingleOrDefault(candidate => candidate.Key == message.TargetKey);
                if (group == null)
                {
                    _logger.LogInformation("Ignoring promotion message for a missing staging group.");
                    return true;
                }

                if (!group.ActivePromotionId.HasValue)
                {
                    _logger.LogInformation("Staging group promotion is not visible yet. Retrying the root message.");
                    return false;
                }

                if (group.ActivePromotionId != message.PromotionId)
                {
                    _logger.LogInformation("Ignoring stale staging group promotion attempt.");
                    return true;
                }

                var stagedPackages = _stagedPackageRepository
                    .GetAll()
                    .Where(candidate => candidate.StagedPackageIdentity.StagingGroupKey == group.Key)
                    .Where(candidate => candidate.StagedPackageIdentity.CurrentStagedPackageKey == candidate.Key)
                    .Where(candidate => candidate.Status == StagedPackageStatus.Promoting)
                    .Where(candidate => candidate.ActivePromotionId == message.PromotionId)
                    .OrderBy(candidate => candidate.Key)
                    .ToList();

                _logger.LogInformation("Fanning out staging group promotion to {PackageCount} package messages.", stagedPackages.Count);
                foreach (var stagedPackage in stagedPackages)
                {
                    await _messageEnqueuer.SendMessageAsync(StagingPromotionMessage.ForPackage(message.PromotionId, stagedPackage.Key));
                }

                _logger.LogInformation("Completed staging group promotion fan-out.");
                return true;
            }
        }
    }
}
