// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.Entities;
using NuGetGallery.Packaging;

namespace NuGetGallery
{
    /// <summary>
    /// Completes symbol promotion without changing parent publication or replacing public symbols.
    /// </summary>
    public class StagedSymbolPackagePromotionService : IStagedSymbolPackagePromotionService
    {
        private readonly IEntityRepository<StagedSymbolPackage> _attempts;
        private readonly IEntityRepository<StagedPackageIdentity> _identities;
        private readonly IEntityRepository<SymbolPackage> _symbols;
        private readonly ICoreSymbolPackageService _symbolService;
        private readonly IStagingBlobService _stagingBlobs;
        private readonly ICoreFileStorageService _storage;
        private readonly ILogger<StagedSymbolPackagePromotionService> _logger;

        public StagedSymbolPackagePromotionService(
            IEntityRepository<StagedSymbolPackage> attempts,
            IEntityRepository<StagedPackageIdentity> identities,
            IEntityRepository<SymbolPackage> symbols,
            ICoreSymbolPackageService symbolService,
            IStagingBlobService stagingBlobs,
            ICoreFileStorageService storage,
            ILogger<StagedSymbolPackagePromotionService> logger)
        {
            _attempts = attempts ?? throw new ArgumentNullException(nameof(attempts));
            _identities = identities ?? throw new ArgumentNullException(nameof(identities));
            _symbols = symbols ?? throw new ArgumentNullException(nameof(symbols));
            _symbolService = symbolService ?? throw new ArgumentNullException(nameof(symbolService));
            _stagingBlobs = stagingBlobs ?? throw new ArgumentNullException(nameof(stagingBlobs));
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
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

            if (!IsEligible(attempt))
            {
                await FailAsync(stagedSymbolPackageKey, promotionId);
                return;
            }

            var hasPublicSymbols = _symbols.GetAll().Any(candidate =>
                candidate.PackageKey == symbol.PackageKey && candidate.Key != symbol.Key && candidate.StatusKey == PackageStatus.Available);
            if (hasPublicSymbols)
            {
                throw new NotSupportedException("Replacing public symbols is not enabled yet.");
            }

            var folder = CoreConstants.Folders.SymbolPackagesFolderName;
            var name = FileNameHelper.BuildFileName(attempt.StagedPackageIdentity.Package, CoreConstants.PackageFileSavePathTemplate, CoreConstants.NuGetSymbolPackageFileExtension);
            var existed = await _storage.FileExistsAsync(folder, name);
            var uri = await _stagingBlobs.GetPackageReadUriAsync(attempt.UploadedBlobPath, attempt.UploadedBlobETag);
            try
            {
                await _storage.CopyFileAsync(uri, folder, name, AccessConditionWrapper.GenerateIfNotExistsCondition());
            }
            catch (FileAlreadyExistsException exception)
            {
                _logger.LogWarning(exception, "Different public symbols already occupy the destination for promotion {PromotionId}.", promotionId);
                await FailAsync(stagedSymbolPackageKey, promotionId);
                return;
            }

            try
            {
                await SetCacheControlAsync(folder, name);
                await _attempts.ExecuteInTransactionAsync(async () =>
                {
                    if (IsEligible(attempt))
                    {
                        await _symbolService.UpdateStatusAsync(symbol, PackageStatus.Available, commitChanges: false);
                        // Retain the attempt until the orchestrator durably records completion.
                        attempt.Status = StagedPackageStatus.Succeeded;
                    }
                    else
                    {
                        _logger.LogWarning("Symbol promotion {PromotionId} lost eligibility during publication.", promotionId);
                        attempt.Status = StagedPackageStatus.PromotionFailed;
                    }

                    await _attempts.CommitChangesAsync();
                });
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to publish staged symbols for promotion {PromotionId}.", promotionId);
                var wasPublished = _symbols.GetAll().AsNoTracking().Any(candidate => candidate.Key == symbol.Key && candidate.StatusKey == PackageStatus.Available);
                if (!existed && !wasPublished)
                {
                    await _storage.DeleteFileAsync(folder, name);
                }

                throw;
            }

            if (attempt.Status == StagedPackageStatus.PromotionFailed && !existed)
            {
                await _storage.DeleteFileAsync(folder, name);
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
            attempt.Status = StagedPackageStatus.PromotionFailed;
            await _attempts.CommitChangesAsync();
        }

        public async Task CleanUpAsync(int stagedSymbolPackageKey, Guid promotionId)
        {
            var attempt = FindAttempt(stagedSymbolPackageKey, promotionId);
            if (attempt == null || attempt.Status != StagedPackageStatus.Succeeded)
            {
                _logger.LogInformation("No successful staged attempt to remove for symbol promotion {PromotionId}.", promotionId);
                return;
            }

            await _attempts.ExecuteInTransactionAsync(async () =>
            {
                var identity = attempt.StagedPackageIdentity;
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
                .Include(attempt => attempt.StagedPackageIdentity.Package)
                .SingleOrDefault(attempt => attempt.Key == key && attempt.ActivePromotionId == promotionId
                    && attempt.StagedPackageIdentity.CurrentStagedSymbolPackageKey == attempt.Key);
            if (attempt?.StagedPackageIdentity.StagingGroupKey.HasValue == true)
            {
                throw new NotSupportedException("Grouped symbol promotion is not enabled yet.");
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
                && candidate.SymbolPackage.StatusKey == PackageStatus.Staged
                && candidate.StagedPackageIdentity.Package.PackageStatusKey == PackageStatus.Available
                && candidate.StagedPackageIdentity.Package.PackageRegistration.Owners.Any(owner => owner.Key == candidate.StagedPackageIdentity.OwnerKey));
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
