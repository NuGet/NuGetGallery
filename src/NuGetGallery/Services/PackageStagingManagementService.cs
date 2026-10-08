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
using NuGetGallery.Authentication;
using NuGetGallery.Configuration;

namespace NuGetGallery
{
    public class PackageStagingManagementService : IPackageStagingManagementService
    {
        private readonly IPackageStagingAuthorizationService _packageStagingAuthorizationService;
        private readonly IPackageService _packageService;
        private readonly IEntityRepository<StagedPackage> _stagedPackageRepository;
        private readonly IEntityRepository<StagingGroup> _stagingGroupRepository;
        private readonly IStagingBlobService _stagingBlobService;
        private readonly IEntityRepository<StagedSymbolPackage> _stagedSymbolPackageRepository;
        private readonly IAppConfiguration _configuration;
        private readonly StagingDeletionService _deletionService;

        public PackageStagingManagementService(
            IPackageStagingAuthorizationService packageStagingAuthorizationService,
            IPackageService packageService,
            IEntityRepository<StagedPackage> stagedPackageRepository,
            IEntityRepository<StagingGroup> stagingGroupRepository,
            IStagingBlobService stagingBlobService,
            IEntityRepository<StagedSymbolPackage> stagedSymbolPackageRepository,
            StagingDeletionService deletionService,
            IAppConfiguration configuration)
        {
            _packageStagingAuthorizationService = packageStagingAuthorizationService ?? throw new ArgumentNullException(nameof(packageStagingAuthorizationService));
            _packageService = packageService ?? throw new ArgumentNullException(nameof(packageService));
            _stagedPackageRepository = stagedPackageRepository ?? throw new ArgumentNullException(nameof(stagedPackageRepository));
            _stagingGroupRepository = stagingGroupRepository ?? throw new ArgumentNullException(nameof(stagingGroupRepository));
            _stagingBlobService = stagingBlobService ?? throw new ArgumentNullException(nameof(stagingBlobService));
            _stagedSymbolPackageRepository = stagedSymbolPackageRepository ?? throw new ArgumentNullException(nameof(stagedSymbolPackageRepository));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _deletionService = deletionService ?? throw new ArgumentNullException(nameof(deletionService));
        }

        public PackageStagingStatus GetPackageStatus(User currentUser, IEnumerable<Scope> scopes, string id, string version)
        {
            if (currentUser == null)
            {
                throw new ArgumentNullException(nameof(currentUser));
            }

            if (scopes == null)
            {
                throw new ArgumentNullException(nameof(scopes));
            }

            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException(CoreStrings.PackageIsMissingRequiredData, nameof(id));
            }

            if (string.IsNullOrWhiteSpace(version))
            {
                throw new ArgumentException(CoreStrings.PackageIsMissingRequiredData, nameof(version));
            }

            var stagedPackage = FindCurrentStagedPackage(id, version);
            if (stagedPackage == null)
            {
                return null;
            }

            if (!_packageStagingAuthorizationService.CanManageWithApiKey(currentUser, scopes, stagedPackage))
            {
                return null;
            }

            return GetStatus(stagedPackage);
        }

        public PackageStagingStatus GetStatus(StagedPackage stagedPackage)
        {
            if (stagedPackage == null)
            {
                throw new ArgumentNullException(nameof(stagedPackage));
            }

            return new PackageStagingStatus
            {
                Id = stagedPackage.StagedPackageIdentity.Package.PackageRegistration.Id,
                Version = stagedPackage.StagedPackageIdentity.Package.NormalizedVersion,
                Status = StagingExpirationPolicy.HasExpired(stagedPackage) ? "Expired" : stagedPackage.Status.ToString(),
                Expires = StagingExpirationPolicy.GetDeadline(stagedPackage).ToUtcIso8601String(),
                Listed = stagedPackage.StagedPackageIdentity.Package.Listed,
            };
        }

        public bool IsEnabled(User currentUser)
        {
            if (currentUser == null)
            {
                throw new ArgumentNullException(nameof(currentUser));
            }

            return _packageStagingAuthorizationService.GetEnabledOwners(currentUser).Count > 0;
        }

        public StagedPackage FindCurrentStagedPackage(string id, string version)
        {
            var package = _packageService.FindPackageByIdAndVersionStrict(id, version);
            if (package?.PackageStatusKey != PackageStatus.Staged)
            {
                return null;
            }

            var stagedPackage = GetCurrentAttempt(package.Key);
            if (stagedPackage?.Status == StagedPackageStatus.Superseded || stagedPackage?.Status == StagedPackageStatus.Deleted)
            {
                return null;
            }

            return stagedPackage;
        }

        public async Task<Stream> OpenPackageContentAsync(StagedPackage stagedPackage)
        {
            if (stagedPackage == null)
            {
                throw new ArgumentNullException(nameof(stagedPackage));
            }

            var path = stagedPackage.UploadedBlobPath;
            var etag = stagedPackage.UploadedBlobETag;
            if (stagedPackage.Status == StagedPackageStatus.Ready)
            {
                path = stagedPackage.ValidatedBlobPath;
                etag = stagedPackage.ValidatedBlobETag;
            }

            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(etag))
            {
                throw new InvalidOperationException($"Staged package attempt '{stagedPackage.Key}' has no downloadable content.");
            }

            return await _stagingBlobService.OpenPackageFileAsync(path, etag);
        }

        public async Task<bool> UpdateListedAsync(StagedPackage stagedPackage, bool listed)
        {
            if (stagedPackage == null)
            {
                throw new ArgumentNullException(nameof(stagedPackage));
            }

            var updated = false;
            try
            {
                await _stagedPackageRepository.ExecuteInTransactionAsync(async () =>
                {
                    var group = stagedPackage.StagedPackageIdentity.StagingGroup;
                    if (stagedPackage.Status == StagedPackageStatus.Promoting || group?.ActivePromotionId.HasValue == true)
                    {
                        return;
                    }

                    stagedPackage.MutationRevision++;
                    if (group != null)
                    {
                        group.MutationRevision++;
                    }

                    stagedPackage.StagedPackageIdentity.Package.Listed = listed;
                    await _stagedPackageRepository.CommitChangesAsync();
                    updated = true;
                });
            }
            catch (DbUpdateConcurrencyException exception)
            {
                exception.Log();
                return false;
            }

            return updated;
        }

        public async Task<bool> DeletePackageAsync(StagedPackage stagedPackage)
        {
            if (stagedPackage == null)
            {
                throw new ArgumentNullException(nameof(stagedPackage));
            }

            var deleted = false;
            try
            {
                await _stagedPackageRepository.ExecuteInTransactionAsync(async () =>
                {
                    var group = stagedPackage.StagedPackageIdentity.StagingGroup;
                    if (stagedPackage.Status == StagedPackageStatus.Promoting || group?.ActivePromotionId.HasValue == true || stagedPackage.StagedPackageIdentity.CurrentStagedSymbolPackage?.Status == StagedPackageStatus.Promoting)
                    {
                        return;
                    }

                    if (stagedPackage.Status == StagedPackageStatus.Deleted)
                    {
                        deleted = true;
                        return;
                    }

                    if (group != null)
                    {
                        group.MutationRevision++;
                    }

                    StagingExpirationPolicy.RefreshGroup(group, StagingExpirationPolicy.CreateDeadline(_configuration));
                    await _deletionService.DeletePackageAsync(stagedPackage);
                    await _stagedPackageRepository.CommitChangesAsync();
                    deleted = true;
                });
            }
            catch (DbUpdateConcurrencyException exception)
            {
                exception.Log();
                return false;
            }

            return deleted;
        }

        public IReadOnlyList<StagedPackage> GetStagedPackages(User currentUser)
        {
            if (currentUser == null)
            {
                throw new ArgumentNullException(nameof(currentUser));
            }

            var ownerKeys = _packageStagingAuthorizationService.GetEnabledOwners(currentUser)
                .Select(owner => owner.Key)
                .ToArray();

            return GetCurrentStagedPackages(ownerKeys)
                .AsEnumerable()
                .Where(stagedPackage => _packageStagingAuthorizationService.CanManage(currentUser, stagedPackage))
                .OrderBy(stagedPackage => stagedPackage.StagedPackageIdentity.Package.PackageRegistration.Id)
                .ThenByDescending(stagedPackage => stagedPackage.UploadedDate)
                .ToList();
        }

        public IReadOnlyList<StagingGroup> GetStagingGroups(User currentUser)
        {
            if (currentUser == null)
            {
                throw new ArgumentNullException(nameof(currentUser));
            }

            var ownerKeys = _packageStagingAuthorizationService.GetEnabledOwners(currentUser)
                .Select(owner => owner.Key)
                .ToArray();

            return _stagingGroupRepository
                .GetAll()
                .Include(group => group.Owner)
                .Where(group => ownerKeys.Contains(group.OwnerKey))
                .OrderBy(group => group.Owner.Username)
                .ThenBy(group => group.Name)
                .ThenBy(group => group.Id)
                .ToList();
        }

        public StagingGroup FindStagingGroup(User stagingOwner, string groupId)
        {
            if (stagingOwner == null)
            {
                throw new ArgumentNullException(nameof(stagingOwner));
            }

            if (string.IsNullOrWhiteSpace(groupId))
            {
                throw new ArgumentException(CoreStrings.PackageIsMissingRequiredData, nameof(groupId));
            }

            return _stagingGroupRepository
                .GetAll()
                .Include(group => group.Owner)
                .Where(group => group.OwnerKey == stagingOwner.Key)
                .ToList()
                .SingleOrDefault(group => string.Equals(group.Id, groupId, StringComparison.OrdinalIgnoreCase));
        }

        public async Task<CreateStagingGroupResult> CreateStagingGroupAsync(User stagingOwner, string groupId, string name)
        {
            if (stagingOwner == null)
            {
                throw new ArgumentNullException(nameof(stagingOwner));
            }

            if (string.IsNullOrWhiteSpace(groupId))
            {
                throw new ArgumentException(CoreStrings.PackageIsMissingRequiredData, nameof(groupId));
            }

            var group = new StagingGroup
            {
                OwnerKey = stagingOwner.Key,
                Owner = stagingOwner,
                Id = groupId,
                Name = string.IsNullOrWhiteSpace(name) ? groupId : name.Trim(),
                CreatedDate = DateTime.UtcNow,
                ExpirationDate = StagingExpirationPolicy.CreateDeadline(_configuration),
            };

            _stagingGroupRepository.InsertOnCommit(group);
            try
            {
                await _stagingGroupRepository.CommitChangesAsync();
            }
            catch (DbUpdateException exception) when (exception.IsSqlUniqueConstraintViolation())
            {
                return CreateStagingGroupResult.GroupAlreadyExists();
            }

            return CreateStagingGroupResult.Created(group);
        }

        public async Task<StagingGroup> RenameStagingGroupAsync(User stagingOwner, string groupId, string name)
        {
            if (stagingOwner == null)
            {
                throw new ArgumentNullException(nameof(stagingOwner));
            }

            if (string.IsNullOrWhiteSpace(groupId))
            {
                throw new ArgumentException(CoreStrings.PackageIsMissingRequiredData, nameof(groupId));
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException(CoreStrings.PackageIsMissingRequiredData, nameof(name));
            }

            var group = FindStagingGroup(stagingOwner, groupId);
            if (group == null || group.ActivePromotionId.HasValue)
            {
                return null;
            }

            try
            {
                group.Name = name.Trim();
                await _stagingGroupRepository.CommitChangesAsync();
            }
            catch (DbUpdateConcurrencyException exception)
            {
                exception.Log();
                return null;
            }

            return group;
        }

        public Task<StagingGroupDeletionResult> DeleteStagingGroupAsync(User stagingOwner, StagingGroup group)
        {
            return DeleteStagingGroupAsync(stagingOwner, group, canDeletePackageIds: null);
        }

        public Task<StagingGroupDeletionResult> DeleteStagingGroupAsync(User stagingOwner, IReadOnlyCollection<Scope> scopes, StagingGroup group)
        {
            ValidateGroupOwner(stagingOwner, group);
            if (scopes == null)
            {
                throw new ArgumentNullException(nameof(scopes));
            }

            if (!stagingOwner.Confirmed || stagingOwner.IsLocked)
            {
                return Task.FromResult(StagingGroupDeletionResult.NotFound());
            }

            var matchingScopes = GetInventoryScopes(stagingOwner, scopes);
            return DeleteStagingGroupAsync(stagingOwner, group, ids => AllowsGroup(matchingScopes, ids));
        }

        private async Task<StagingGroupDeletionResult> DeleteStagingGroupAsync(User stagingOwner, StagingGroup group, Func<IEnumerable<string>, bool> canDeletePackageIds)
        {
            ValidateGroupOwner(stagingOwner, group);
            StagingGroupDeletionResult result = null;
            try
            {
                await _stagingGroupRepository.ExecuteInTransactionAsync(async () =>
                {
                    result = await _deletionService.DeleteGroupAsync(group, canDeletePackageIds);
                });
            }
            catch (DbUpdateConcurrencyException exception)
            {
                exception.Log();
                result = StagingGroupDeletionResult.Conflict(result?.AffectedPackageCount ?? 0);
            }

            return result;
        }

        private static void ValidateGroupOwner(User stagingOwner, StagingGroup group)
        {
            if (stagingOwner == null)
            {
                throw new ArgumentNullException(nameof(stagingOwner));
            }

            if (group == null)
            {
                throw new ArgumentNullException(nameof(group));
            }

            if (group.OwnerKey != stagingOwner.Key)
            {
                throw new ArgumentException("The staging group must belong to the authorized owner.");
            }
        }

        public async Task<StagingGroupMembershipResult> AddPackageToStagingGroupAsync(User stagingOwner, StagingGroup group, StagedPackage stagedPackage)
        {
            if (stagingOwner == null)
            {
                throw new ArgumentNullException(nameof(stagingOwner));
            }

            if (group == null)
            {
                throw new ArgumentNullException(nameof(group));
            }

            if (stagedPackage == null)
            {
                throw new ArgumentNullException(nameof(stagedPackage));
            }

            return await MovePackageIdentityAsync(stagingOwner, stagedPackage.StagedPackageIdentity, group);
        }

        public async Task<StagingGroupMembershipResult> RemovePackageFromStagingGroupAsync(User stagingOwner, StagedPackage stagedPackage)
        {
            if (stagingOwner == null)
            {
                throw new ArgumentNullException(nameof(stagingOwner));
            }

            if (stagedPackage == null)
            {
                throw new ArgumentNullException(nameof(stagedPackage));
            }

            return await MovePackageIdentityAsync(stagingOwner, stagedPackage.StagedPackageIdentity, group: null);
        }

        public async Task<StagingGroupMembershipResult> MovePackageIdentityAsync(User stagingOwner, StagedPackageIdentity identity, StagingGroup group)
        {
            if (stagingOwner == null)
            {
                throw new ArgumentNullException(nameof(stagingOwner));
            }

            if (identity == null)
            {
                throw new ArgumentNullException(nameof(identity));
            }

            if (identity.OwnerKey != stagingOwner.Key || (group != null && group.OwnerKey != stagingOwner.Key))
            {
                throw new ArgumentException("The staging identity and group must belong to the authorized owner.");
            }

            if (identity.StagingGroupKey == group?.Key)
            {
                return StagingGroupMembershipResult.Unchanged;
            }

            if (identity.CurrentStagedPackage?.Status == StagedPackageStatus.Promoting || identity.CurrentStagedSymbolPackage?.Status == StagedPackageStatus.Promoting || identity.StagingGroup?.ActivePromotionId.HasValue == true || group?.ActivePromotionId.HasValue == true)
            {
                return StagingGroupMembershipResult.Conflict;
            }

            try
            {
                await _stagedPackageRepository.ExecuteInTransactionAsync(async () =>
                {
                    StagingExpirationPolicy.EnsureMutable(identity, group);
                    var deadline = StagingExpirationPolicy.CreateDeadline(_configuration);
                    if (identity.CurrentStagedPackage != null)
                    {
                        identity.CurrentStagedPackage.MutationRevision++;
                    }

                    if (identity.CurrentStagedSymbolPackage != null)
                    {
                        identity.CurrentStagedSymbolPackage.MutationRevision++;
                    }

                    if (identity.StagingGroup != null)
                    {
                        identity.StagingGroup.MutationRevision++;
                        StagingExpirationPolicy.RefreshGroup(identity.StagingGroup, deadline);
                    }

                    if (group != null)
                    {
                        group.MutationRevision++;
                        StagingExpirationPolicy.RefreshGroup(group, deadline);
                    }

                    if (identity.CurrentStagedPackage != null)
                    {
                        identity.CurrentStagedPackage.ExpirationDate = deadline;
                    }

                    if (identity.CurrentStagedSymbolPackage != null)
                    {
                        identity.CurrentStagedSymbolPackage.ExpirationDate = deadline;
                    }

                    identity.StagingGroupKey = group?.Key;
                    identity.StagingGroup = group;
                    await _stagedPackageRepository.CommitChangesAsync();
                });
            }
            catch (DbUpdateConcurrencyException exception)
            {
                exception.Log();
                return StagingGroupMembershipResult.Conflict;
            }
            catch (StagingExpiredException exception)
            {
                exception.Log();
                return StagingGroupMembershipResult.Conflict;
            }

            return StagingGroupMembershipResult.Updated;
        }

        public IReadOnlyList<StagingGroupSummary> GetStagingGroupSummaries(User stagingOwner)
        {
            if (stagingOwner == null)
            {
                throw new ArgumentNullException(nameof(stagingOwner));
            }

            var groups = _stagingGroupRepository
                .GetAll()
                .Include(group => group.Owner)
                .Where(group => group.OwnerKey == stagingOwner.Key)
                .ToList();
            var packagesByGroup = GetCurrentStagedPackages(new[] { stagingOwner.Key })
                .Where(package => package.StagedPackageIdentity.StagingGroupKey.HasValue)
                .ToLookup(package => package.StagedPackageIdentity.StagingGroupKey.Value);
            var symbolsByGroup = GetCurrentStagedSymbols(new[] { stagingOwner.Key })
                .Where(symbol => symbol.StagedPackageIdentity.StagingGroupKey.HasValue)
                .ToLookup(symbol => symbol.StagedPackageIdentity.StagingGroupKey.Value);

            return groups
                .Select(group => new StagingGroupSummary(group, packagesByGroup[group.Key].ToList(), symbolsByGroup[group.Key].ToList()))
                .ToList();
        }

        public StagingGroupSummaryPage GetStagingGroupSummaryPage(User stagingOwner, IReadOnlyCollection<Scope> scopes, int page, int pageSize)
        {
            if (stagingOwner == null)
            {
                throw new ArgumentNullException(nameof(stagingOwner));
            }

            if (scopes == null)
            {
                throw new ArgumentNullException(nameof(scopes));
            }

            if (page <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(page));
            }

            if (pageSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(pageSize));
            }

            if (!stagingOwner.Confirmed || stagingOwner.IsLocked)
            {
                return new StagingGroupSummaryPage(Array.Empty<StagingGroupSummary>(), 0);
            }

            var groupsQuery = _stagingGroupRepository
                .GetAll()
                .Include(group => group.Owner)
                .Where(group => group.OwnerKey == stagingOwner.Key);

            var matchingScopes = GetInventoryScopes(stagingOwner, scopes);
            if (matchingScopes.Count == 0)
            {
                return new StagingGroupSummaryPage(Array.Empty<StagingGroupSummary>(), 0);
            }

            if (!matchingScopes.Any(scope => scope.Subject == NuGetPackagePattern.AllInclusivePattern))
            {
                var identities = GetGroupedIdentities(stagingOwner);
                var visibleIds = GetVisiblePackageIds(identities.Select(identity => identity.Package.PackageRegistration.Id), matchingScopes);
                var blockedGroupKeys = identities.Where(identity => !visibleIds.Contains(identity.Package.PackageRegistration.Id))
                    .Select(identity => identity.StagingGroupKey.Value).Distinct().ToArray();
                groupsQuery = groupsQuery.Where(group => !blockedGroupKeys.Contains(group.Key));
            }

            var totalCount = groupsQuery.Count();
            var skip = ((long)page - 1) * pageSize;

            var groups = new List<StagingGroup>();
            if (skip < totalCount)
            {
                groups = groupsQuery
                    .OrderByDescending(group => group.CreatedDate)
                    .ThenBy(group => group.Id)
                    .Skip((int)skip)
                    .Take(pageSize)
                    .ToList();
            }

            var groupKeys = groups.Select(group => group.Key).ToArray();
            var packagesByGroup = GetCurrentStagedPackages(new[] { stagingOwner.Key })
                .Where(package => package.StagedPackageIdentity.StagingGroupKey.HasValue)
                .Where(package => groupKeys.Contains(package.StagedPackageIdentity.StagingGroupKey.Value))
                .ToLookup(package => package.StagedPackageIdentity.StagingGroupKey.Value);
            var symbolsByGroup = GetCurrentStagedSymbols(new[] { stagingOwner.Key })
                .Where(symbol => symbol.StagedPackageIdentity.StagingGroupKey.HasValue)
                .Where(symbol => groupKeys.Contains(symbol.StagedPackageIdentity.StagingGroupKey.Value))
                .ToLookup(symbol => symbol.StagedPackageIdentity.StagingGroupKey.Value);

            var summaries = groups
                .Select(group => new StagingGroupSummary(group, packagesByGroup[group.Key].ToList(), symbolsByGroup[group.Key].ToList()))
                .ToList();

            return new StagingGroupSummaryPage(summaries, totalCount);
        }

        public StagingGroupPackagePage GetStagingGroupPackagePage(User stagingOwner, IReadOnlyCollection<Scope> scopes, string groupId, int page, int pageSize)
        {
            if (stagingOwner == null)
            {
                throw new ArgumentNullException(nameof(stagingOwner));
            }

            if (scopes == null)
            {
                throw new ArgumentNullException(nameof(scopes));
            }

            if (string.IsNullOrWhiteSpace(groupId))
            {
                throw new ArgumentException(CoreStrings.PackageIsMissingRequiredData, nameof(groupId));
            }

            if (page <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(page));
            }

            if (pageSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(pageSize));
            }

            if (!stagingOwner.Confirmed || stagingOwner.IsLocked)
            {
                return null;
            }

            var group = FindStagingGroup(stagingOwner, groupId);
            if (group == null)
            {
                return null;
            }

            var ids = GetGroupedIdentities(stagingOwner).Where(identity => identity.StagingGroupKey == group.Key)
                .Select(identity => identity.Package.PackageRegistration.Id).Distinct().AsEnumerable();
            if (!AllowsGroup(GetInventoryScopes(stagingOwner, scopes), ids))
            {
                return null;
            }

            var packagesQuery = GetCurrentStagedPackages(new[] { stagingOwner.Key })
                .Where(package => package.StagedPackageIdentity.StagingGroupKey == group.Key);
            var symbolsQuery = GetCurrentStagedSymbols(new[] { stagingOwner.Key })
                .Where(symbol => symbol.StagedPackageIdentity.StagingGroupKey == group.Key);
            var symbolCount = symbolsQuery.Count();
            var totalCount = packagesQuery.Count() + symbolCount;
            var hasRegistrationOwnershipLoss = packagesQuery.Any(package => !package.StagedPackageIdentity.Package.PackageRegistration.Owners.Any(owner => owner.Key == package.StagedPackageIdentity.OwnerKey))
                || symbolsQuery.Any(symbol => !symbol.StagedPackageIdentity.Package.PackageRegistration.Owners.Any(owner => owner.Key == symbol.StagedPackageIdentity.OwnerKey));
            var hasLockedRegistration = packagesQuery.Any(package => package.StagedPackageIdentity.Package.PackageRegistration.IsLocked)
                || symbolsQuery.Any(symbol => symbol.StagedPackageIdentity.Package.PackageRegistration.IsLocked);
            var allPackagesReady = !hasRegistrationOwnershipLoss && !hasLockedRegistration && !packagesQuery.Any(package => package.Status != StagedPackageStatus.Ready);

            if (allPackagesReady)
            {
                var symbolReadiness = symbolsQuery.Select(symbol => new
                {
                    IsReady = symbol.Status == StagedPackageStatus.Ready && symbol.SymbolPackage.StatusKey == PackageStatus.Staged,
                    ParentIsAvailable = symbol.StagedPackageIdentity.Package.PackageStatusKey == PackageStatus.Available,
                    ParentIsReady = symbol.StagedPackageIdentity.Package.PackageStatusKey == PackageStatus.Staged
                        && symbol.StagedPackageIdentity.CurrentStagedPackageKey.HasValue
                        && symbol.StagedPackageIdentity.CurrentStagedPackage.Status == StagedPackageStatus.Ready,
                });
                allPackagesReady = !symbolReadiness.Any(symbol => !symbol.IsReady || (!symbol.ParentIsAvailable && !symbol.ParentIsReady));
            }

            var skip = ((long)page - 1) * pageSize;

            var packages = new List<StagedPackage>();
            var symbols = new List<StagedSymbolPackage>();
            if (skip < totalCount)
            {
                var pageItems = packagesQuery.Select(package => new { package.Key, package.UploadedDate, IsSymbol = false })
                    .Concat(symbolsQuery.Select(symbol => new { symbol.Key, symbol.UploadedDate, IsSymbol = true }))
                    .OrderByDescending(item => item.UploadedDate)
                    .ThenByDescending(item => item.Key)
                    .ThenBy(item => item.IsSymbol)
                    .Skip((int)skip)
                    .Take(pageSize)
                    .ToList();
                var packageKeys = pageItems.Where(item => !item.IsSymbol).Select(item => item.Key).ToArray();
                var symbolKeys = pageItems.Where(item => item.IsSymbol).Select(item => item.Key).ToArray();
                packages = packagesQuery.Where(package => packageKeys.Contains(package.Key)).OrderByDescending(package => package.UploadedDate).ThenByDescending(package => package.Key).ToList();
                symbols = symbolsQuery.Where(symbol => symbolKeys.Contains(symbol.Key)).OrderByDescending(symbol => symbol.UploadedDate).ThenByDescending(symbol => symbol.Key).ToList();
            }

            return new StagingGroupPackagePage(group, packages, totalCount, allPackagesReady, symbols, symbolCount, hasRegistrationOwnershipLoss, hasLockedRegistration);
        }

        /// <inheritdoc />
        public StagingArtifactPage<StagedPackage> GetStagedPackagePage(User stagingOwner, IReadOnlyCollection<Scope> scopes, int page, int pageSize)
        {
            ValidateArtifactPaging(stagingOwner, scopes, page, pageSize);
            if (!stagingOwner.Confirmed || stagingOwner.IsLocked)
            {
                return new StagingArtifactPage<StagedPackage>(Array.Empty<StagedPackage>(), 0);
            }

            var query = GetCurrentStagedPackages(new[] { stagingOwner.Key });
            var matchingScopes = GetInventoryScopes(stagingOwner, scopes);
            if (!matchingScopes.Any(scope => scope.Subject == NuGetPackagePattern.AllInclusivePattern))
            {
                var ids = GetVisiblePackageIds(query.Select(package => package.StagedPackageIdentity.Package.PackageRegistration.Id), matchingScopes);
                query = query.Where(package => ids.Contains(package.StagedPackageIdentity.Package.PackageRegistration.Id));
            }

            var totalCount = query.Count();
            var skip = ((long)page - 1) * pageSize;
            var items = new List<StagedPackage>();
            if (skip < totalCount)
            {
                items = query.OrderByDescending(package => package.UploadedDate)
                    .ThenByDescending(package => package.Key)
                    .Skip((int)skip)
                    .Take(pageSize)
                    .ToList();
            }

            return new StagingArtifactPage<StagedPackage>(items, totalCount);
        }

        /// <inheritdoc />
        public StagingArtifactPage<StagedSymbolPackage> GetStagedSymbolPackagePage(User stagingOwner, IReadOnlyCollection<Scope> scopes, int page, int pageSize)
        {
            ValidateArtifactPaging(stagingOwner, scopes, page, pageSize);
            if (!stagingOwner.Confirmed || stagingOwner.IsLocked)
            {
                return new StagingArtifactPage<StagedSymbolPackage>(Array.Empty<StagedSymbolPackage>(), 0);
            }

            var query = GetCurrentStagedSymbols(new[] { stagingOwner.Key })
                .Where(symbol => symbol.Status != StagedPackageStatus.Deleted && symbol.Status != StagedPackageStatus.Superseded);
            var matchingScopes = GetInventoryScopes(stagingOwner, scopes);
            if (!matchingScopes.Any(scope => scope.Subject == NuGetPackagePattern.AllInclusivePattern))
            {
                var ids = GetVisiblePackageIds(query.Select(symbol => symbol.StagedPackageIdentity.Package.PackageRegistration.Id), matchingScopes);
                query = query.Where(symbol => ids.Contains(symbol.StagedPackageIdentity.Package.PackageRegistration.Id));
            }

            var totalCount = query.Count();
            var skip = ((long)page - 1) * pageSize;
            var items = new List<StagedSymbolPackage>();
            if (skip < totalCount)
            {
                items = query.OrderByDescending(symbol => symbol.UploadedDate)
                    .ThenByDescending(symbol => symbol.Key)
                    .Skip((int)skip)
                    .Take(pageSize)
                    .ToList();
            }

            return new StagingArtifactPage<StagedSymbolPackage>(items, totalCount);
        }

        /// <inheritdoc />
        public StagedPackage GetStagedPackage(User stagingOwner, IReadOnlyCollection<Scope> scopes, string id, string version)
        {
            var package = FindApiPackage(stagingOwner, scopes, id, version);
            if (package == null || !stagingOwner.Confirmed || stagingOwner.IsLocked)
            {
                return null;
            }

            return GetCurrentStagedPackages(new[] { stagingOwner.Key })
                .SingleOrDefault(attempt => attempt.StagedPackageIdentityKey == package.Key);
        }

        /// <inheritdoc />
        public StagedSymbolPackage GetStagedSymbolPackage(User stagingOwner, IReadOnlyCollection<Scope> scopes, string id, string version)
        {
            var package = FindApiPackage(stagingOwner, scopes, id, version);
            if (package == null || !stagingOwner.Confirmed || stagingOwner.IsLocked)
            {
                return null;
            }

            return GetCurrentStagedSymbols(new[] { stagingOwner.Key })
                .Where(symbol => symbol.Status != StagedPackageStatus.Deleted && symbol.Status != StagedPackageStatus.Superseded)
                .SingleOrDefault(attempt => attempt.StagedPackageIdentityKey == package.Key);
        }

        private Package FindApiPackage(User stagingOwner, IReadOnlyCollection<Scope> scopes, string id, string version)
        {
            if (stagingOwner == null)
            {
                throw new ArgumentNullException(nameof(stagingOwner));
            }

            if (scopes == null)
            {
                throw new ArgumentNullException(nameof(scopes));
            }

            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException(CoreStrings.PackageIsMissingRequiredData, nameof(id));
            }

            if (string.IsNullOrWhiteSpace(version))
            {
                throw new ArgumentException(CoreStrings.PackageIsMissingRequiredData, nameof(version));
            }

            var package = _packageService.FindPackageByIdAndVersionStrict(id, version);
            if (package == null || !GetInventoryScopes(stagingOwner, scopes).Any(scope => scope.AllowsSubject(package.PackageRegistration.Id)))
            {
                return null;
            }

            return package;
        }

        private static List<Scope> GetInventoryScopes(User stagingOwner, IReadOnlyCollection<Scope> scopes)
        {
            return scopes.Where(scope => scope.OwnerKey == stagingOwner.Key && scope.AllowsActions(NuGetScopes.PackageStage)).ToList();
        }

        private static bool AllowsGroup(IReadOnlyCollection<Scope> scopes, IEnumerable<string> packageIds)
        {
            return scopes.Count > 0 && (scopes.Any(scope => scope.Subject == NuGetPackagePattern.AllInclusivePattern)
                || packageIds.All(id => scopes.Any(scope => scope.AllowsSubject(id))));
        }

        private IQueryable<StagedPackageIdentity> GetGroupedIdentities(User stagingOwner)
        {
            var ownerKeys = new[] { stagingOwner.Key };
            return GetCurrentStagedPackages(ownerKeys)
                .Where(package => package.StagedPackageIdentity.StagingGroupKey.HasValue)
                .Select(package => package.StagedPackageIdentity)
                .Concat(GetCurrentStagedSymbols(ownerKeys)
                    .Where(symbol => symbol.StagedPackageIdentity.StagingGroupKey.HasValue)
                    .Select(symbol => symbol.StagedPackageIdentity));
        }

        private static string[] GetVisiblePackageIds(IQueryable<string> query, IReadOnlyCollection<Scope> scopes)
        {
            return query.Distinct().AsEnumerable()
                .Where(id => scopes.Any(scope => scope.AllowsSubject(id)))
                .ToArray();
        }

        private static void ValidateArtifactPaging(User stagingOwner, IReadOnlyCollection<Scope> scopes, int page, int pageSize)
        {
            if (stagingOwner == null)
            {
                throw new ArgumentNullException(nameof(stagingOwner));
            }

            if (scopes == null)
            {
                throw new ArgumentNullException(nameof(scopes));
            }

            if (page < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(page));
            }

            if (pageSize < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(pageSize));
            }
        }

        public IReadOnlyList<PackageStagingStatus> GetPackages(User currentUser, IEnumerable<Scope> scopes)
        {
            if (currentUser == null)
            {
                throw new ArgumentNullException(nameof(currentUser));
            }

            if (scopes == null)
            {
                throw new ArgumentNullException(nameof(scopes));
            }

            var ownerKeys = _packageStagingAuthorizationService.GetEnabledOwners(currentUser)
                .Select(owner => owner.Key)
                .ToArray();

            return GetCurrentStagedPackages(ownerKeys)
                .AsEnumerable()
                .Where(stagedPackage => _packageStagingAuthorizationService.CanManageWithApiKey(currentUser, scopes, stagedPackage))
                .Select(GetStatus)
                .OrderBy(package => package.Id)
                .ThenBy(package => package.Version)
                .ToList();
        }

        private IQueryable<StagedPackage> GetCurrentStagedPackages(int[] ownerKeys)
        {
            return _stagedPackageRepository
                .GetAll()
                .Include(stagedPackage => stagedPackage.StagedPackageIdentity.Package.PackageRegistration.Owners)
                .Include(stagedPackage => stagedPackage.StagedPackageIdentity.Owner)
                .Include(stagedPackage => stagedPackage.StagedPackageIdentity.StagingGroup)
                .Include(stagedPackage => stagedPackage.StagedPackageIdentity.CurrentStagedSymbolPackage.SymbolPackage)
                .Where(stagedPackage => ownerKeys.Contains(stagedPackage.StagedPackageIdentity.OwnerKey))
                .Where(stagedPackage => stagedPackage.StagedPackageIdentity.Package.PackageStatusKey == PackageStatus.Staged
                    || (stagedPackage.StagedPackageIdentity.StagingGroupKey.HasValue && stagedPackage.Status == StagedPackageStatus.Succeeded))
                .Where(stagedPackage => stagedPackage.Status != StagedPackageStatus.Superseded && stagedPackage.Status != StagedPackageStatus.Deleted)
                .Where(stagedPackage => stagedPackage.StagedPackageIdentity.CurrentStagedPackageKey == stagedPackage.Key);
        }

        private IQueryable<StagedSymbolPackage> GetCurrentStagedSymbols(int[] ownerKeys)
        {
            return _stagedSymbolPackageRepository.GetAll()
                .Include(symbol => symbol.SymbolPackage)
                .Include(symbol => symbol.StagedPackageIdentity.Owner)
                .Include(symbol => symbol.StagedPackageIdentity.StagingGroup)
                .Include(symbol => symbol.StagedPackageIdentity.CurrentStagedPackage)
                .Include(symbol => symbol.StagedPackageIdentity.Package.PackageRegistration.Owners)
                .Include(symbol => symbol.StagedPackageIdentity.Package.SymbolPackages)
                .Where(symbol => ownerKeys.Contains(symbol.StagedPackageIdentity.OwnerKey))
                .Where(symbol => symbol.StagedPackageIdentity.CurrentStagedSymbolPackageKey == symbol.Key)
                .Where(symbol => symbol.SymbolPackage.StatusKey == PackageStatus.Staged
                    || (symbol.StagedPackageIdentity.StagingGroupKey.HasValue && symbol.Status == StagedPackageStatus.Succeeded)
                    || (symbol.StagedPackageIdentity.StagingGroupKey.HasValue
                        && symbol.Status == StagedPackageStatus.Promoting && symbol.SymbolPackage.StatusKey == PackageStatus.Available));
        }

        private StagedPackage GetCurrentAttempt(int packageKey)
        {
            return _stagedPackageRepository
                .GetAll()
                .Include(stagedPackage => stagedPackage.StagedPackageIdentity.Owner)
                .Include(stagedPackage => stagedPackage.StagedPackageIdentity.StagingGroup)
                .Include(stagedPackage => stagedPackage.StagedPackageIdentity.CurrentStagedSymbolPackage.SymbolPackage)
                .SingleOrDefault(stagedPackage => stagedPackage.StagedPackageIdentityKey == packageKey && stagedPackage.StagedPackageIdentity.CurrentStagedPackageKey == stagedPackage.Key);
        }

    }
}
