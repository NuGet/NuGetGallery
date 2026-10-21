// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    /// <summary>
    /// Shares private staging deletion and blob cleanup within the caller's SQL transaction.
    /// </summary>
    public class StagingDeletionService
    {
        private readonly IEntityRepository<StagedPackage> _packages;
        private readonly IEntityRepository<StagedSymbolPackage> _symbols;
        private readonly IEntityRepository<StagedPackageIdentity> _identities;
        private readonly IEntityRepository<SymbolPackage> _symbolPackages;
        private readonly IEntityRepository<StagingGroup> _groups;
        private readonly ICorePackageService _packageService;
        private readonly IStagingBlobCleanupService _blobCleanup;

        public StagingDeletionService(
            IEntityRepository<StagedPackage> packages,
            IEntityRepository<StagedSymbolPackage> symbols,
            IEntityRepository<StagedPackageIdentity> identities,
            IEntityRepository<SymbolPackage> symbolPackages,
            IEntityRepository<StagingGroup> groups,
            ICorePackageService packageService,
            IStagingBlobCleanupService blobCleanup)
        {
            _packages = packages ?? throw new ArgumentNullException(nameof(packages));
            _symbols = symbols ?? throw new ArgumentNullException(nameof(symbols));
            _identities = identities ?? throw new ArgumentNullException(nameof(identities));
            _symbolPackages = symbolPackages ?? throw new ArgumentNullException(nameof(symbolPackages));
            _groups = groups ?? throw new ArgumentNullException(nameof(groups));
            _packageService = packageService ?? throw new ArgumentNullException(nameof(packageService));
            _blobCleanup = blobCleanup ?? throw new ArgumentNullException(nameof(blobCleanup));
        }

        /// <summary>
        /// Retires a private parent and retains its symbols in a fresh waiting attempt.
        /// The caller checks eligibility and commits the surrounding transaction.
        /// </summary>
        public async Task DeletePackageAsync(StagedPackage attempt)
        {
            if (attempt == null)
            {
                throw new ArgumentNullException(nameof(attempt));
            }

            _blobCleanup.QueuePackageFiles(attempt.StagedPackageIdentityKey);
            attempt.Status = StagedPackageStatus.Deleted;
            attempt.StagedPackageIdentity.Package.Listed = false;
            await _packageService.UpdatePackageStatusAsync(attempt.StagedPackageIdentity.Package, PackageStatus.Deleted, commitChanges: false);
            await StagedSymbolPackageRenewal.RenewAsync(attempt.StagedPackageIdentity, StagedPackageStatus.WaitingForParent, _symbols);
        }

        /// <summary>
        /// Removes private symbols and their attempts without changing public content.
        /// The caller checks eligibility and commits the surrounding transaction.
        /// </summary>
        public async Task DeleteSymbolPackageAsync(StagedSymbolPackage attempt)
        {
            if (attempt == null)
            {
                throw new ArgumentNullException(nameof(attempt));
            }

            var identity = attempt.StagedPackageIdentity;
            _blobCleanup.QueueSymbolFiles(identity.Key);
            if (!identity.CurrentStagedPackageKey.HasValue)
            {
                _blobCleanup.QueuePackageFiles(identity.Key);
            }

            identity.CurrentStagedSymbolPackageKey = null;
            identity.CurrentStagedSymbolPackage = null;
            var attempts = _symbols.GetAll()
                .Include(symbol => symbol.SymbolPackage)
                .Where(symbol => symbol.StagedPackageIdentityKey == identity.Key && symbol.SymbolPackage.StatusKey == PackageStatus.Staged)
                .Select(symbol => new { Attempt = symbol, SymbolPackage = symbol.SymbolPackage })
                .ToList();
            foreach (var symbol in attempts)
            {
                _symbols.DeleteOnCommit(symbol.Attempt);
            }

            // Break the current-attempt circular reference before deleting ordinary symbol rows.
            await _symbols.CommitChangesAsync();
            if (!identity.CurrentStagedPackageKey.HasValue)
            {
                _identities.DeleteOnCommit(identity);
            }

            foreach (var symbol in attempts.Select(symbol => symbol.SymbolPackage).Distinct())
            {
                _symbolPackages.DeleteOnCommit(symbol);
            }
            await _symbols.CommitChangesAsync();
        }

        /// <summary>
        /// Removes a group and its private members, preserving published content and accepted promotions.
        /// The caller commits the surrounding transaction.
        /// </summary>
        public async Task<StagingGroupDeletionResult> DeleteGroupAsync(StagingGroup group)
        {
            if (group == null)
            {
                throw new ArgumentNullException(nameof(group));
            }

            var groupedAttempts = _packages.GetAll()
                .Include(package => package.StagedPackageIdentity.Package.PackageRegistration)
                .Where(package => package.StagedPackageIdentity.OwnerKey == group.OwnerKey)
                .Where(package => package.StagedPackageIdentity.StagingGroupKey == group.Key)
                .Where(package => package.StagedPackageIdentity.CurrentStagedPackageKey == package.Key)
                .ToList();
            var stagedPackages = groupedAttempts
                .Where(package => package.StagedPackageIdentity.Package.PackageStatusKey == PackageStatus.Staged)
                .Where(package => package.Status != StagedPackageStatus.Superseded && package.Status != StagedPackageStatus.Deleted)
                .ToList();
            var stagedSymbols = _symbols.GetAll()
                .Include(symbol => symbol.SymbolPackage)
                .Where(symbol => symbol.StagedPackageIdentity.OwnerKey == group.OwnerKey && symbol.StagedPackageIdentity.StagingGroupKey == group.Key)
                .Where(symbol => symbol.StagedPackageIdentity.CurrentStagedSymbolPackageKey == symbol.Key)
                .Where(symbol => symbol.SymbolPackage.StatusKey == PackageStatus.Staged || symbol.Status == StagedPackageStatus.Succeeded || (symbol.Status == StagedPackageStatus.Promoting && symbol.SymbolPackage.StatusKey == PackageStatus.Available))
                .ToList();
            var affectedCount = stagedPackages.Count + stagedSymbols.Count;
            if (group.ActivePromotionId.HasValue || groupedAttempts.Any(package => package.Status == StagedPackageStatus.Promoting) || stagedSymbols.Any(symbol => symbol.Status == StagedPackageStatus.Promoting))
            {
                return StagingGroupDeletionResult.Conflict(affectedCount);
            }

            var symbolMembers = _symbols.GetAll()
                .Where(symbol => symbol.StagedPackageIdentity.OwnerKey == group.OwnerKey && symbol.StagedPackageIdentity.StagingGroupKey == group.Key)
                .Where(symbol => symbol.SymbolPackage.StatusKey == PackageStatus.Staged)
                .Select(symbol => new { Attempt = symbol, Identity = symbol.StagedPackageIdentity, SymbolPackage = symbol.SymbolPackage })
                .ToList();
            foreach (var identity in symbolMembers.Select(member => member.Identity).Distinct())
            {
                _blobCleanup.QueueSymbolFiles(identity.Key);
                if (!identity.CurrentStagedPackageKey.HasValue)
                {
                    _blobCleanup.QueuePackageFiles(identity.Key);
                }
            }

            foreach (var package in stagedPackages)
            {
                _blobCleanup.QueuePackageFiles(package.StagedPackageIdentityKey);
            }

            foreach (var member in symbolMembers)
            {
                member.Identity.CurrentStagedSymbolPackageKey = null;
                member.Identity.CurrentStagedSymbolPackage = null;
                member.Identity.StagingGroupKey = null;
                member.Identity.StagingGroup = null;
                _symbols.DeleteOnCommit(member.Attempt);
            }

            // Break the current-symbol circular references before deleting their ordinary rows.
            if (symbolMembers.Count > 0)
            {
                await _groups.CommitChangesAsync();
            }

            foreach (var identity in symbolMembers.Select(member => member.Identity).Distinct())
            {
                if (!identity.CurrentStagedPackageKey.HasValue)
                {
                    _identities.DeleteOnCommit(identity);
                }
            }

            foreach (var symbol in symbolMembers.Select(member => member.SymbolPackage).Distinct())
            {
                _symbolPackages.DeleteOnCommit(symbol);
            }

            var stagedPackageKeys = new HashSet<int>(stagedPackages.Select(package => package.Key));
            foreach (var attempt in groupedAttempts)
            {
                var identity = attempt.StagedPackageIdentity;
                identity.StagingGroupKey = null;
                identity.StagingGroup = null;
                if (stagedPackageKeys.Contains(attempt.Key))
                {
                    attempt.Status = StagedPackageStatus.Deleted;
                    identity.Package.Listed = false;
                    await _packageService.UpdatePackageStatusAsync(identity.Package, PackageStatus.Deleted, commitChanges: false);
                }
            }

            _groups.DeleteOnCommit(group);
            await _groups.CommitChangesAsync();
            return StagingGroupDeletionResult.Deleted(affectedCount);
        }
    }
}
