// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    /// <summary>
    /// Provides owner-scoped access and management for current staged symbol attempts.
    /// </summary>
    public class SymbolPackageStagingManagementService : ISymbolPackageStagingManagementService
    {
        private readonly IPackageStagingAuthorizationService _authorizationService;
        private readonly IPackageService _packageService;
        private readonly IEntityRepository<StagedSymbolPackage> _stagedSymbolPackageRepository;
        private readonly IEntityRepository<StagedPackageIdentity> _identityRepository;
        private readonly IEntityRepository<SymbolPackage> _symbolPackageRepository;
        private readonly IStagingBlobService _stagingBlobService;

        public SymbolPackageStagingManagementService(
            IPackageStagingAuthorizationService authorizationService,
            IPackageService packageService,
            IEntityRepository<StagedSymbolPackage> stagedSymbolPackageRepository,
            IEntityRepository<StagedPackageIdentity> identityRepository,
            IEntityRepository<SymbolPackage> symbolPackageRepository,
            IStagingBlobService stagingBlobService)
        {
            _authorizationService = authorizationService ?? throw new ArgumentNullException(nameof(authorizationService));
            _packageService = packageService ?? throw new ArgumentNullException(nameof(packageService));
            _stagedSymbolPackageRepository = stagedSymbolPackageRepository ?? throw new ArgumentNullException(nameof(stagedSymbolPackageRepository));
            _identityRepository = identityRepository ?? throw new ArgumentNullException(nameof(identityRepository));
            _symbolPackageRepository = symbolPackageRepository ?? throw new ArgumentNullException(nameof(symbolPackageRepository));
            _stagingBlobService = stagingBlobService ?? throw new ArgumentNullException(nameof(stagingBlobService));
        }

        public IReadOnlyList<StagedSymbolPackage> GetStagedSymbolPackages(User currentUser)
        {
            if (currentUser == null)
            {
                throw new ArgumentNullException(nameof(currentUser));
            }

            var ownerKeys = _authorizationService.GetEnabledOwners(currentUser)
                .Select(owner => owner.Key)
                .ToArray();

            return GetCurrentAttempts()
                .Where(attempt => ownerKeys.Contains(attempt.StagedPackageIdentity.OwnerKey))
                .AsEnumerable()
                .Where(attempt => _authorizationService.CanManage(currentUser, attempt))
                .OrderBy(attempt => attempt.StagedPackageIdentity.Package.PackageRegistration.Id)
                .ThenByDescending(attempt => attempt.UploadedDate)
                .ToList();
        }

        public StagedSymbolPackage FindCurrentStagedSymbolPackage(string id, string version)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException(CoreStrings.PackageIsMissingRequiredData, nameof(id));
            }

            if (string.IsNullOrWhiteSpace(version))
            {
                throw new ArgumentException(CoreStrings.PackageIsMissingRequiredData, nameof(version));
            }

            var package = _packageService.FindPackageByIdAndVersionStrict(id, version);
            if (package == null)
            {
                return null;
            }

            return GetCurrentAttempts()
                .SingleOrDefault(attempt => attempt.StagedPackageIdentityKey == package.Key);
        }

        public Task<Stream> OpenPackageContentAsync(StagedSymbolPackage stagedSymbolPackage)
        {
            if (stagedSymbolPackage == null)
            {
                throw new ArgumentNullException(nameof(stagedSymbolPackage));
            }

            var path = stagedSymbolPackage.UploadedBlobPath;
            var etag = stagedSymbolPackage.UploadedBlobETag;
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(etag))
            {
                throw new InvalidOperationException($"Staged symbol attempt '{stagedSymbolPackage.Key}' has no downloadable content.");
            }

            return _stagingBlobService.OpenPackageFileAsync(path, etag);
        }

        public async Task<bool> DeletePackageAsync(StagedSymbolPackage stagedSymbolPackage)
        {
            if (stagedSymbolPackage == null)
            {
                throw new ArgumentNullException(nameof(stagedSymbolPackage));
            }

            var identity = stagedSymbolPackage.StagedPackageIdentity;
            var symbolPackage = stagedSymbolPackage.SymbolPackage;
            if (identity.CurrentStagedSymbolPackageKey != stagedSymbolPackage.Key || symbolPackage.StatusKey != PackageStatus.Staged)
            {
                return false;
            }

            var group = identity.StagingGroup;
            if (stagedSymbolPackage.Status == StagedPackageStatus.Promoting || group?.ActivePromotionId.HasValue == true)
            {
                return false;
            }

            try
            {
                await _stagedSymbolPackageRepository.ExecuteInTransactionAsync(async () =>
                {
                    if (group != null)
                    {
                        group.MutationRevision++;
                    }

                    identity.CurrentStagedSymbolPackageKey = null;
                    identity.CurrentStagedSymbolPackage = null;
                    var attempts = _stagedSymbolPackageRepository.GetAll()
                        .Include(attempt => attempt.SymbolPackage)
                        .Where(attempt => attempt.StagedPackageIdentityKey == identity.Key && attempt.SymbolPackage.StatusKey == PackageStatus.Staged)
                        .Select(attempt => new { Attempt = attempt, SymbolPackage = attempt.SymbolPackage })
                        .ToList();
                    foreach (var attempt in attempts)
                    {
                        _stagedSymbolPackageRepository.DeleteOnCommit(attempt.Attempt);
                    }

                    // Save first to break the circular reference between the identity and its current attempt.
                    await _stagedSymbolPackageRepository.CommitChangesAsync();

                    if (!identity.CurrentStagedPackageKey.HasValue)
                    {
                        _identityRepository.DeleteOnCommit(identity);
                    }

                    foreach (var attempt in attempts)
                    {
                        _symbolPackageRepository.DeleteOnCommit(attempt.SymbolPackage);
                    }
                    await _stagedSymbolPackageRepository.CommitChangesAsync();
                });
            }
            catch (DbUpdateConcurrencyException exception)
            {
                exception.Log();
                return false;
            }

            return true;
        }

        private IQueryable<StagedSymbolPackage> GetCurrentAttempts()
        {
            return _stagedSymbolPackageRepository
                .GetAll()
                .Include(attempt => attempt.SymbolPackage)
                .Include(attempt => attempt.StagedPackageIdentity.Owner)
                .Include(attempt => attempt.StagedPackageIdentity.CurrentStagedPackage)
                .Include(attempt => attempt.StagedPackageIdentity.StagingGroup)
                .Include(attempt => attempt.StagedPackageIdentity.Package.PackageRegistration.Owners)
                .Where(attempt => attempt.StagedPackageIdentity.CurrentStagedSymbolPackageKey == attempt.Key)
                .Where(attempt => attempt.SymbolPackage.StatusKey == PackageStatus.Staged);
        }
    }
}
