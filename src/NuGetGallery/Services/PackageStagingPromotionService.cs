// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using NuGet.Services.Entities;
using NuGet.Services.Staging;

namespace NuGetGallery
{
    /// <summary>
    /// Accepts and enqueues package and staging group promotion requests.
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

            var promotionId = Guid.NewGuid();
            try
            {
                stagedPackage.ActivePromotionId = promotionId;
                stagedPackage.PromotionMessageSentDate = DateTime.UtcNow;
                stagedPackage.Status = StagedPackageStatus.Promoting;
                await _stagedPackageRepository.CommitChangesAsync();
            }
            catch (DbUpdateConcurrencyException exception)
            {
                exception.Log();
                return PackageStagingPromotionResult.Conflict;
            }

            if (!await TrySendMessageAsync(StagingPromotionMessage.ForPackage(promotionId, stagedPackage.Key)))
            {
                return await MakePackageImmediatelyRetryableAsync(stagedPackage);
            }

            return PackageStagingPromotionResult.Accepted;
        }

        /// <inheritdoc />
        public async Task<PackageStagingPromotionResult> ResendPackageAsync(User currentUser, StagedPackage stagedPackage)
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

            if (stagedPackage.Status != StagedPackageStatus.Promoting || !stagedPackage.ActivePromotionId.HasValue)
            {
                return PackageStagingPromotionResult.NotReady;
            }

            if (!StagingPromotionResendPolicy.IsDue(stagedPackage.PromotionMessageSentDate))
            {
                return PackageStagingPromotionResult.NotReady;
            }

            var promotionId = stagedPackage.ActivePromotionId.Value;
            try
            {
                stagedPackage.PromotionMessageSentDate = DateTime.UtcNow;
                await _stagedPackageRepository.CommitChangesAsync();
            }
            catch (DbUpdateConcurrencyException exception)
            {
                exception.Log();
                return PackageStagingPromotionResult.Conflict;
            }

            if (!await TrySendMessageAsync(StagingPromotionMessage.ForPackage(promotionId, stagedPackage.Key)))
            {
                return await MakePackageImmediatelyRetryableAsync(stagedPackage);
            }

            return PackageStagingPromotionResult.Accepted;
        }

        /// <inheritdoc />
        public async Task<StagingGroupPromotionResult> PromoteGroupAsync(User currentUser, StagingGroup group)
        {
            if (currentUser == null)
            {
                throw new ArgumentNullException(nameof(currentUser));
            }

            if (group == null)
            {
                throw new ArgumentNullException(nameof(group));
            }

            if (!_authorizationService.GetEnabledOwners(currentUser).Any(owner => owner.Key == group.OwnerKey))
            {
                return StagingGroupPromotionResult.Unauthorized;
            }

            var stagedPackages = _stagedPackageRepository
                .GetAll()
                .Include(stagedPackage => stagedPackage.StagedPackageIdentity.Package.PackageRegistration.Owners)
                .Include(stagedPackage => stagedPackage.StagedPackageIdentity.Owner)
                .Where(stagedPackage => stagedPackage.StagedPackageIdentity.StagingGroupKey == group.Key)
                .Where(stagedPackage => stagedPackage.StagedPackageIdentity.CurrentStagedPackageKey == stagedPackage.Key)
                .Where(stagedPackage => stagedPackage.StagedPackageIdentity.Package.PackageStatusKey == PackageStatus.Staged)
                .Where(stagedPackage => stagedPackage.Status != StagedPackageStatus.Superseded && stagedPackage.Status != StagedPackageStatus.Deleted)
                .ToList();
            if (stagedPackages.Count == 0)
            {
                return StagingGroupPromotionResult.Empty;
            }

            if (stagedPackages.Any(stagedPackage => !_authorizationService.CanManage(currentUser, stagedPackage)))
            {
                return StagingGroupPromotionResult.Unauthorized;
            }

            if (stagedPackages.Any(stagedPackage => stagedPackage.Status != StagedPackageStatus.Ready))
            {
                return StagingGroupPromotionResult.NotReady;
            }

            if (group.ActivePromotionId.HasValue)
            {
                return StagingGroupPromotionResult.Conflict;
            }

            var promotionId = Guid.NewGuid();
            try
            {
                group.ActivePromotionId = promotionId;
                group.PromotionMessageSentDate = DateTime.UtcNow;
                foreach (var stagedPackage in stagedPackages)
                {
                    stagedPackage.ActivePromotionId = promotionId;
                    stagedPackage.Status = StagedPackageStatus.Promoting;
                }

                await _stagedPackageRepository.CommitChangesAsync();
            }
            catch (DbUpdateConcurrencyException exception)
            {
                exception.Log();
                return StagingGroupPromotionResult.Conflict;
            }

            if (!await TrySendMessageAsync(StagingPromotionMessage.ForGroup(promotionId, group.Key)))
            {
                return await MakeGroupImmediatelyRetryableAsync(group);
            }

            return StagingGroupPromotionResult.Accepted;
        }

        /// <inheritdoc />
        public async Task<StagingGroupPromotionResult> ResendGroupAsync(User currentUser, StagingGroup group)
        {
            if (currentUser == null)
            {
                throw new ArgumentNullException(nameof(currentUser));
            }

            if (group == null)
            {
                throw new ArgumentNullException(nameof(group));
            }

            if (!_authorizationService.GetEnabledOwners(currentUser).Any(owner => owner.Key == group.OwnerKey))
            {
                return StagingGroupPromotionResult.Unauthorized;
            }

            if (!group.ActivePromotionId.HasValue || !StagingPromotionResendPolicy.IsDue(group.PromotionMessageSentDate))
            {
                return StagingGroupPromotionResult.NotReady;
            }

            var promotionId = group.ActivePromotionId.Value;
            try
            {
                group.PromotionMessageSentDate = DateTime.UtcNow;
                await _stagedPackageRepository.CommitChangesAsync();
            }
            catch (DbUpdateConcurrencyException exception)
            {
                exception.Log();
                return StagingGroupPromotionResult.Conflict;
            }

            if (!await TrySendMessageAsync(StagingPromotionMessage.ForGroup(promotionId, group.Key)))
            {
                return await MakeGroupImmediatelyRetryableAsync(group);
            }

            return StagingGroupPromotionResult.Accepted;
        }

        private async Task<bool> TrySendMessageAsync(StagingPromotionMessage message)
        {
            try
            {
                await _messageEnqueuer.SendMessageAsync(message);
                return true;
            }
            catch (ServiceBusException exception)
            {
                exception.Log();
            }
            catch (TimeoutException exception)
            {
                exception.Log();
            }
            catch (OperationCanceledException exception)
            {
                exception.Log();
            }

            return false;
        }

        private async Task<PackageStagingPromotionResult> MakePackageImmediatelyRetryableAsync(StagedPackage stagedPackage)
        {
            try
            {
                stagedPackage.PromotionMessageSentDate = StagingPromotionResendPolicy.GetRetryableSentDate();
                await _stagedPackageRepository.CommitChangesAsync();
            }
            catch (DbUpdateConcurrencyException exception)
            {
                exception.Log();
                return PackageStagingPromotionResult.Conflict;
            }

            return PackageStagingPromotionResult.DispatchFailed;
        }

        private async Task<StagingGroupPromotionResult> MakeGroupImmediatelyRetryableAsync(StagingGroup group)
        {
            try
            {
                group.PromotionMessageSentDate = StagingPromotionResendPolicy.GetRetryableSentDate();
                await _stagedPackageRepository.CommitChangesAsync();
            }
            catch (DbUpdateConcurrencyException exception)
            {
                exception.Log();
                return StagingGroupPromotionResult.Conflict;
            }

            return StagingGroupPromotionResult.DispatchFailed;
        }
    }
}
