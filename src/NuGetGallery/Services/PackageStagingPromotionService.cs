// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.Entity.Infrastructure;
using System.Threading.Tasks;
using NuGet.Services.Staging;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    /// <summary>
    /// Accepts and enqueues package promotion requests.
    /// </summary>
    public class PackageStagingPromotionService : IPackageStagingPromotionService
    {
        private readonly IPackageStagingAuthorizationService _authorizationService;
        private readonly IStagingPromotionMessageEnqueuer _messageEnqueuer;
        private readonly IEntityRepository<StagedPackage> _stagedPackageRepository;

        /// <summary>
        /// Initializes a new instance of the <see cref="PackageStagingPromotionService"/> class.
        /// </summary>
        /// <param name="authorizationService">The staged-package authorization service.</param>
        /// <param name="messageEnqueuer">The promotion message enqueuer.</param>
        /// <param name="stagedPackageRepository">The staged-package repository.</param>
        public PackageStagingPromotionService(
            IPackageStagingAuthorizationService authorizationService,
            IStagingPromotionMessageEnqueuer messageEnqueuer,
            IEntityRepository<StagedPackage> stagedPackageRepository)
        {
            _authorizationService = authorizationService ?? throw new ArgumentNullException(nameof(authorizationService));
            _messageEnqueuer = messageEnqueuer ?? throw new ArgumentNullException(nameof(messageEnqueuer));
            _stagedPackageRepository = stagedPackageRepository ?? throw new ArgumentNullException(nameof(stagedPackageRepository));
        }

        /// <inheritdoc />
        public async Task<PackageStagingPromotionResult> PromotePackageAsync(User currentUser, StagedPackage stagedPackage)
        {
            if (currentUser == null)
            {
                throw new ArgumentNullException(nameof(currentUser));
            }

            if (stagedPackage == null)
            {
                throw new ArgumentNullException(nameof(stagedPackage));
            }

            if (!_authorizationService.CanManage(currentUser, stagedPackage))
            {
                return PackageStagingPromotionResult.Unauthorized;
            }

            if (stagedPackage.StagedPackageIdentity.StagingGroupKey.HasValue)
            {
                return PackageStagingPromotionResult.Grouped;
            }

            if (stagedPackage.Status != StagedPackageStatus.Ready)
            {
                return PackageStagingPromotionResult.NotReady;
            }

            try
            {
                await _stagedPackageRepository.ExecuteInTransactionAsync(async () =>
                {
                    var promotionId = Guid.NewGuid();
                    stagedPackage.ActivePromotionId = promotionId;
                    stagedPackage.Status = StagedPackageStatus.Promoting;
                    await _stagedPackageRepository.CommitChangesAsync();

                    await _messageEnqueuer.SendMessageAsync(StagingPromotionMessage.ForPackage(promotionId, stagedPackage.Key));
                });
            }
            catch (DbUpdateConcurrencyException)
            {
                return PackageStagingPromotionResult.Conflict;
            }

            return PackageStagingPromotionResult.Accepted;
        }
    }
}
