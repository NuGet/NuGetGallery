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
    /// Accepts and enqueues package, symbol, and staging group promotion requests.
    /// </summary>
    public class PackageStagingPromotionService : IPackageStagingPromotionService
    {
        private readonly IPackageStagingAuthorizationService _authorizationService;
        private readonly IStagingPromotionMessageEnqueuer _messageEnqueuer;
        private readonly IEntityRepository<StagedPackage> _stagedPackageRepository;
        private readonly IEntityRepository<StagedSymbolPackage> _stagedSymbolPackageRepository;

        /// <summary>
        /// Initializes a new instance of the <see cref="PackageStagingPromotionService"/> class.
        /// </summary>
        /// <param name="authorizationService">The staged-package authorization service.</param>
        /// <param name="messageEnqueuer">The promotion message enqueuer.</param>
        /// <param name="stagedPackageRepository">The staged-package repository.</param>
        /// <param name="stagedSymbolPackageRepository">The staged-symbol repository.</param>
        public PackageStagingPromotionService(
            IPackageStagingAuthorizationService authorizationService,
            IStagingPromotionMessageEnqueuer messageEnqueuer,
            IEntityRepository<StagedPackage> stagedPackageRepository,
            IEntityRepository<StagedSymbolPackage> stagedSymbolPackageRepository)
        {
            _authorizationService = authorizationService ?? throw new ArgumentNullException(nameof(authorizationService));
            _messageEnqueuer = messageEnqueuer ?? throw new ArgumentNullException(nameof(messageEnqueuer));
            _stagedPackageRepository = stagedPackageRepository ?? throw new ArgumentNullException(nameof(stagedPackageRepository));
            _stagedSymbolPackageRepository = stagedSymbolPackageRepository ?? throw new ArgumentNullException(nameof(stagedSymbolPackageRepository));
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

            var symbols = _stagedSymbolPackageRepository.GetAll()
                .Include(attempt => attempt.SymbolPackage)
                .Include(attempt => attempt.StagedPackageIdentity.Package)
                .SingleOrDefault(attempt => attempt.Key == stagedPackage.StagedPackageIdentity.CurrentStagedSymbolPackageKey);
            if (symbols != null && (!_authorizationService.CanManage(currentUser, symbols) || StagedSymbolPackagePromotionEligibility.GetBlockers(symbols, stagedPackage).Count > 0))
            {
                symbols = null;
            }

            var promotionId = Guid.NewGuid();
            try
            {
                stagedPackage.ActivePromotionId = promotionId;
                stagedPackage.PromotionMessageSentDate = DateTime.UtcNow;
                stagedPackage.Status = StagedPackageStatus.Promoting;
                if (symbols != null)
                {
                    symbols.ActivePromotionId = promotionId;
                    symbols.Status = StagedPackageStatus.Promoting;
                    symbols.PromotionMessageSentDate = null;
                }

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
        public async Task<PackageStagingPromotionResult> PromoteSymbolPackageAsync(User currentUser, StagedSymbolPackage stagedSymbolPackage)
        {
            if (currentUser == null)
            {
                throw new ArgumentNullException(nameof(currentUser));
            }

            if (stagedSymbolPackage == null)
            {
                throw new ArgumentNullException(nameof(stagedSymbolPackage));
            }

            if (!_authorizationService.CanManage(currentUser, stagedSymbolPackage))
            {
                return PackageStagingPromotionResult.Unauthorized;
            }

            if (stagedSymbolPackage.StagedPackageIdentity.StagingGroupKey.HasValue)
            {
                return PackageStagingPromotionResult.Grouped;
            }

            if (StagedSymbolPackagePromotionEligibility.GetBlockers(stagedSymbolPackage).Count > 0)
            {
                return PackageStagingPromotionResult.NotReady;
            }

            stagedSymbolPackage.ActivePromotionId = Guid.NewGuid();
            stagedSymbolPackage.Status = StagedPackageStatus.Promoting;
            return await SendSymbolPromotionAsync(stagedSymbolPackage);
        }

        /// <inheritdoc />
        public async Task<PackageStagingPromotionResult> ResendSymbolPackageAsync(User currentUser, StagedSymbolPackage stagedSymbolPackage)
        {
            if (currentUser == null)
            {
                throw new ArgumentNullException(nameof(currentUser));
            }

            if (stagedSymbolPackage == null)
            {
                throw new ArgumentNullException(nameof(stagedSymbolPackage));
            }

            if (!_authorizationService.CanManage(currentUser, stagedSymbolPackage))
            {
                return PackageStagingPromotionResult.Unauthorized;
            }

            if (stagedSymbolPackage.StagedPackageIdentity.StagingGroupKey.HasValue)
            {
                return PackageStagingPromotionResult.Grouped;
            }

            if (!StagedSymbolPackagePromotionEligibility.CanResend(stagedSymbolPackage))
            {
                return PackageStagingPromotionResult.NotReady;
            }

            return await SendSymbolPromotionAsync(stagedSymbolPackage);
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
            var stagedSymbols = _stagedSymbolPackageRepository.GetAll()
                .Include(symbol => symbol.SymbolPackage)
                .Include(symbol => symbol.StagedPackageIdentity.Package.PackageRegistration.Owners)
                .Include(symbol => symbol.StagedPackageIdentity.Owner)
                .Where(symbol => symbol.StagedPackageIdentity.StagingGroupKey == group.Key)
                .Where(symbol => symbol.StagedPackageIdentity.CurrentStagedSymbolPackageKey == symbol.Key)
                .ToList();

            if (stagedPackages.Count + stagedSymbols.Count == 0)
            {
                return StagingGroupPromotionResult.Empty;
            }

            if (stagedPackages.Any(stagedPackage => !_authorizationService.CanManage(currentUser, stagedPackage)))
            {
                return StagingGroupPromotionResult.Unauthorized;
            }

            if (stagedSymbols.Any(symbol => !_authorizationService.CanManage(currentUser, symbol)))
            {
                return StagingGroupPromotionResult.Unauthorized;
            }

            if (stagedPackages.Any(stagedPackage => stagedPackage.Status != StagedPackageStatus.Ready))
            {
                return StagingGroupPromotionResult.NotReady;
            }

            foreach (var symbol in stagedSymbols)
            {
                var parent = stagedPackages.SingleOrDefault(package => package.StagedPackageIdentityKey == symbol.StagedPackageIdentityKey);
                var blockers = StagedSymbolPackagePromotionEligibility.GetBlockers(symbol, parent, forGroup: true);
                if (blockers.Count > 0)
                {
                    return StagingGroupPromotionResult.NotReady;
                }
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

                foreach (var symbol in stagedSymbols)
                {
                    symbol.ActivePromotionId = promotionId;
                    symbol.Status = StagedPackageStatus.Promoting;
                    symbol.PromotionMessageSentDate = null;
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

        private async Task<PackageStagingPromotionResult> SendSymbolPromotionAsync(StagedSymbolPackage attempt)
        {
            var promotionId = attempt.ActivePromotionId.Value;
            try
            {
                attempt.PromotionMessageSentDate = DateTime.UtcNow;
                await _stagedSymbolPackageRepository.CommitChangesAsync();
            }
            catch (DbUpdateConcurrencyException exception)
            {
                exception.Log();
                return PackageStagingPromotionResult.Conflict;
            }

            if (await TrySendMessageAsync(StagingPromotionMessage.ForSymbolPackage(promotionId, attempt.Key)))
            {
                return PackageStagingPromotionResult.Accepted;
            }

            try
            {
                attempt.PromotionMessageSentDate = StagingPromotionResendPolicy.GetRetryableSentDate();
                await _stagedSymbolPackageRepository.CommitChangesAsync();
            }
            catch (DbUpdateConcurrencyException exception)
            {
                exception.Log();
                return PackageStagingPromotionResult.Conflict;
            }

            return PackageStagingPromotionResult.DispatchFailed;
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
