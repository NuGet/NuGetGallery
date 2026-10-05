// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.Entities;
using NuGetGallery.Packaging;

namespace NuGetGallery
{
    /// <summary>
    /// Completes symbol promotion and replaces older symbols without changing parent publication.
    /// </summary>
    public class StagedSymbolPackagePromotionService : IStagedSymbolPackagePromotionService
    {
        private readonly IEntityRepository<StagedSymbolPackage> _attempts;
        private readonly IEntityRepository<StagedPackageIdentity> _identities;
        private readonly IEntityRepository<SymbolPackage> _symbols;
        private readonly ICoreSymbolPackageService _symbolService;
        private readonly IStagingBlobService _stagingBlobs;
        private readonly ICoreFileStorageService _storage;
        private readonly IStagingGroupPromotionService _groups;
        private readonly IStagingBlobCleanupService _blobCleanup;
        private readonly IStagingPromotionNotificationService _notifications;
        private readonly ILogger<StagedSymbolPackagePromotionService> _logger;

        public StagedSymbolPackagePromotionService(
            IEntityRepository<StagedSymbolPackage> attempts,
            IEntityRepository<StagedPackageIdentity> identities,
            IEntityRepository<SymbolPackage> symbols,
            ICoreSymbolPackageService symbolService,
            IStagingBlobService stagingBlobs,
            ICoreFileStorageService storage,
            IStagingGroupPromotionService groups,
            IStagingBlobCleanupService blobCleanup,
            IStagingPromotionNotificationService notifications,
            ILogger<StagedSymbolPackagePromotionService> logger)
        {
            _attempts = attempts ?? throw new ArgumentNullException(nameof(attempts));
            _identities = identities ?? throw new ArgumentNullException(nameof(identities));
            _symbols = symbols ?? throw new ArgumentNullException(nameof(symbols));
            _symbolService = symbolService ?? throw new ArgumentNullException(nameof(symbolService));
            _stagingBlobs = stagingBlobs ?? throw new ArgumentNullException(nameof(stagingBlobs));
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _groups = groups ?? throw new ArgumentNullException(nameof(groups));
            _blobCleanup = blobCleanup ?? throw new ArgumentNullException(nameof(blobCleanup));
            _notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task CompleteAsync(int stagedSymbolPackageKey, Guid promotionId)
        {
            var attempt = FindAttempt(stagedSymbolPackageKey, promotionId);
            if (attempt == null || attempt.Status != StagedPackageStatus.Promoting)
            {
                _logger.LogInformation("Symbol promotion {PromotionId} for attempt {AttemptKey} is no longer pending.", promotionId, stagedSymbolPackageKey);
                return;
            }

            var symbol = attempt.SymbolPackage;
            if (symbol.PackageKey != attempt.StagedPackageIdentity.Package.Key)
            {
                throw new InvalidOperationException("The staged symbol does not belong to its staging identity's parent.");
            }

            if (attempt.StagedPackageIdentity.StagingGroupKey.HasValue && IsPublished(symbol))
            {
                _logger.LogInformation("Grouped symbol promotion {PromotionId} is already published; awaiting orchestration completion.", promotionId);
                return;
            }

            if (!IsEligible(attempt))
            {
                await FailAsync(stagedSymbolPackageKey, promotionId);
                return;
            }

            var publicSymbols = _symbols.GetAll()
                .Where(candidate => candidate.PackageKey == symbol.PackageKey && candidate.Key != symbol.Key && candidate.StatusKey == PackageStatus.Available)
                .ToList();
            var folder = CoreConstants.Folders.SymbolPackagesFolderName;
            var name = FileNameHelper.BuildFileName(attempt.StagedPackageIdentity.Package, CoreConstants.PackageFileSavePathTemplate, CoreConstants.NuGetSymbolPackageFileExtension);
            var existed = await _storage.FileExistsAsync(folder, name);
            var condition = AccessConditionWrapper.GenerateIfNotExistsCondition();
            var matched = false;
            if (publicSymbols.Count > 0)
            {
                var current = await _storage.GetFileReferenceAsync(folder, name);
                if (current != null)
                {
                    using (var content = current.OpenRead())
                    {
                        matched = MatchesContent(symbol, content);
                        if (!matched && !publicSymbols.Any(previous => MatchesContent(previous, content)))
                        {
                            _logger.LogWarning("The public symbol file no longer matches the published symbol rows for promotion {PromotionId}.", promotionId);
                            await FailAsync(stagedSymbolPackageKey, promotionId);
                            return;
                        }
                    }

                    condition = AccessConditionWrapper.GenerateIfMatchCondition(current.ContentId);
                }
                else
                {
                    _logger.LogInformation("Recreating the missing public symbol file for replacement promotion {PromotionId}.", promotionId);
                }
            }

            var copied = false;
            if (!matched)
            {
                var uri = await _stagingBlobs.GetPackageReadUriAsync(attempt.UploadedBlobPath, attempt.UploadedBlobETag);
                try
                {
                    await _storage.CopyFileAsync(uri, folder, name, condition);
                    copied = publicSymbols.Count > 0 || !existed;
                }
                catch (FileAlreadyExistsException exception)
                {
                    if (!await MatchesPublicContentAsync(symbol, folder, name))
                    {
                        _logger.LogWarning(exception, "Different public symbols already occupy the destination for promotion {PromotionId}.", promotionId);
                        await FailAsync(stagedSymbolPackageKey, promotionId);
                        return;
                    }

                    _logger.LogInformation("Resuming symbol promotion {PromotionId} with an identical existing public copy.", promotionId);
                }
            }

            try
            {
                await SetCacheControlAsync(folder, name);
                await _attempts.ExecuteInTransactionAsync(async () =>
                {
                    if (IsEligible(attempt))
                    {
                        foreach (var publicSymbol in publicSymbols)
                        {
                            await _symbolService.UpdateStatusAsync(publicSymbol, PackageStatus.Deleted, commitChanges: false);
                        }

                        await _symbolService.UpdateStatusAsync(symbol, PackageStatus.Available, commitChanges: false);
                        // Grouped attempts remain Promoting until orchestration completion is durable.
                        if (!attempt.StagedPackageIdentity.StagingGroupKey.HasValue)
                        {
                            attempt.Status = StagedPackageStatus.Succeeded;
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Symbol promotion {PromotionId} lost eligibility during publication.", promotionId);
                        if (copied)
                        {
                            await _storage.DeleteFileAsync(folder, name);
                        }

                        if (!attempt.StagedPackageIdentity.StagingGroupKey.HasValue)
                        {
                            attempt.Status = StagedPackageStatus.PromotionFailed;
                        }
                    }

                    await _attempts.CommitChangesAsync();
                });
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to publish staged symbols for promotion {PromotionId}.", promotionId);
                var wasPublished = IsPublished(symbol);
                if (copied && !wasPublished)
                {
                    await _storage.DeleteFileAsync(folder, name);
                }

                throw;
            }
        }

        public async Task FailAsync(int stagedSymbolPackageKey, Guid promotionId)
        {
            var attempt = FindAttempt(stagedSymbolPackageKey, promotionId);
            if (attempt == null || attempt.Status != StagedPackageStatus.Promoting)
            {
                _logger.LogInformation("Ignoring failure for inactive symbol promotion {PromotionId}.", promotionId);
                return;
            }

            _logger.LogWarning("Symbol promotion {PromotionId} failed; retaining staged content and leaving the parent unchanged.", promotionId);
            if (attempt.StagedPackageIdentity.StagingGroupKey.HasValue)
            {
                // The orchestrator records its terminal outcome before group finalization can remove this attempt.
                return;
            }

            attempt.Status = StagedPackageStatus.PromotionFailed;
            await _attempts.CommitChangesAsync();
        }

        public async Task CleanUpAsync(int stagedSymbolPackageKey, Guid promotionId)
        {
            var attempt = FindAttempt(stagedSymbolPackageKey, promotionId);
            if (attempt?.StagedPackageIdentity.StagingGroupKey.HasValue == true)
            {
                var owner = attempt.StagedPackageIdentity.Owner;
                StagingPromotionArtifact artifact = null;
                if (attempt.Status == StagedPackageStatus.Promoting)
                {
                    if (IsPublished(attempt.SymbolPackage))
                    {
                        attempt.Status = StagedPackageStatus.Succeeded;
                    }
                    else
                    {
                        attempt.Status = StagedPackageStatus.PromotionFailed;
                    }

                    artifact = new StagingPromotionArtifact(attempt.StagedPackageIdentity.Package, true, attempt.Status == StagedPackageStatus.Succeeded);
                    await _attempts.CommitChangesAsync();
                }

                await _groups.TryFinalizeAsync(attempt.StagedPackageIdentity.StagingGroupKey.Value, promotionId);
                if (artifact != null)
                {
                    await _notifications.SendAsync(owner, artifact);
                }

                return;
            }

            if (attempt == null || (attempt.Status != StagedPackageStatus.Succeeded && attempt.Status != StagedPackageStatus.PromotionFailed))
            {
                _logger.LogInformation("No terminal staged attempt to finalize for symbol promotion {PromotionId}.", promotionId);
                return;
            }

            var stagingOwner = attempt.StagedPackageIdentity.Owner;
            var result = new StagingPromotionArtifact(attempt.StagedPackageIdentity.Package, true, attempt.Status == StagedPackageStatus.Succeeded);
            if (attempt.Status == StagedPackageStatus.PromotionFailed)
            {
                attempt.ActivePromotionId = null;
                attempt.PromotionMessageSentDate = null;
                await _attempts.CommitChangesAsync();
                await _notifications.SendAsync(stagingOwner, result);
                return;
            }

            await _attempts.ExecuteInTransactionAsync(async () =>
            {
                var identity = attempt.StagedPackageIdentity;
                _blobCleanup.QueueSymbolFiles(identity.Key);
                if (!identity.CurrentStagedPackageKey.HasValue)
                {
                    _blobCleanup.QueuePackageFiles(identity.Key);
                }

                identity.CurrentStagedSymbolPackageKey = null;
                identity.CurrentStagedSymbolPackage = null;
                _attempts.DeleteOnCommit(attempt);
                await _attempts.CommitChangesAsync();

                if (!identity.CurrentStagedPackageKey.HasValue)
                {
                    _identities.DeleteOnCommit(identity);
                    await _attempts.CommitChangesAsync();
                }
            });
            await _notifications.SendAsync(stagingOwner, result);
        }

        private StagedSymbolPackage FindAttempt(int key, Guid promotionId)
        {
            if (key <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(key));
            }

            if (promotionId == Guid.Empty)
            {
                throw new ArgumentOutOfRangeException(nameof(promotionId));
            }

            var attempt = _attempts.GetAll()
                .Include(attempt => attempt.SymbolPackage)
                .Include(attempt => attempt.StagedPackageIdentity.Package.PackageRegistration)
                .Include(attempt => attempt.StagedPackageIdentity.Owner)
                .Include(attempt => attempt.StagedPackageIdentity.StagingGroup)
                .SingleOrDefault(attempt => attempt.Key == key && attempt.ActivePromotionId == promotionId
                    && attempt.StagedPackageIdentity.CurrentStagedSymbolPackageKey == attempt.Key);
            if (attempt?.Status == StagedPackageStatus.Promoting
                && attempt.StagedPackageIdentity.StagingGroupKey.HasValue
                && attempt.StagedPackageIdentity.StagingGroup?.ActivePromotionId != promotionId)
            {
                _logger.LogInformation("Ignoring inactive group symbol promotion {PromotionId}.", promotionId);
                return null;
            }

            return attempt;
        }

        private bool IsEligible(StagedSymbolPackage attempt)
        {
            return _attempts.GetAll().AsNoTracking().Any(candidate =>
                candidate.Key == attempt.Key
                && candidate.ActivePromotionId == attempt.ActivePromotionId
                && candidate.Status == StagedPackageStatus.Promoting
                && candidate.StagedPackageIdentity.CurrentStagedSymbolPackageKey == candidate.Key
                && (!candidate.StagedPackageIdentity.StagingGroupKey.HasValue || candidate.StagedPackageIdentity.StagingGroup.ActivePromotionId == candidate.ActivePromotionId)
                && candidate.SymbolPackage.StatusKey == PackageStatus.Staged
                && candidate.StagedPackageIdentity.Package.PackageStatusKey == PackageStatus.Available
                && candidate.StagedPackageIdentity.Package.PackageRegistration.Owners.Any(owner => owner.Key == candidate.StagedPackageIdentity.OwnerKey));
        }

        private bool IsPublished(SymbolPackage symbol)
        {
            return _symbols.GetAll().AsNoTracking().Any(candidate => candidate.Key == symbol.Key && candidate.StatusKey == PackageStatus.Available);
        }

        private async Task<bool> MatchesPublicContentAsync(SymbolPackage symbol, string folder, string name)
        {
            using (var content = await _storage.GetFileAsync(folder, name))
            {
                if (content == null)
                {
                    throw new InvalidOperationException($"The public symbol package '{folder}/{name}' disappeared while checking the promotion copy.");
                }

                return MatchesContent(symbol, content);
            }
        }

        private static bool MatchesContent(SymbolPackage symbol, Stream content)
        {
            content.Position = 0;
            return content.Length == symbol.FileSize && CryptographyService.GenerateHash(content, symbol.HashAlgorithm) == symbol.Hash;
        }

        private Task SetCacheControlAsync(string folder, string name)
        {
            return _storage.SetPropertiesAsync(folder, name, (_, properties) =>
            {
                if (properties.CacheControl == CoreConstants.DefaultCacheControl)
                {
                    return Task.FromResult(false);
                }

                properties.CacheControl = CoreConstants.DefaultCacheControl;
                return Task.FromResult(true);
            });
        }
    }
}
