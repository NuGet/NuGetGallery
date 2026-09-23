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
    public class PackageStagingManagementService : IPackageStagingManagementService
    {
        private readonly IPackageStagingAuthorizationService _packageStagingAuthorizationService;
        private readonly IPackageService _packageService;
        private readonly IEntityRepository<StagedPackage> _stagedPackageRepository;
        private readonly IEntityRepository<StagingGroup> _stagingGroupRepository;
        private readonly IStagingBlobService _stagingBlobService;

        public PackageStagingManagementService(
            IPackageStagingAuthorizationService packageStagingAuthorizationService,
            IPackageService packageService,
            IEntityRepository<StagedPackage> stagedPackageRepository,
            IEntityRepository<StagingGroup> stagingGroupRepository,
            IStagingBlobService stagingBlobService)
        {
            _packageStagingAuthorizationService = packageStagingAuthorizationService ?? throw new ArgumentNullException(nameof(packageStagingAuthorizationService));
            _packageService = packageService ?? throw new ArgumentNullException(nameof(packageService));
            _stagedPackageRepository = stagedPackageRepository ?? throw new ArgumentNullException(nameof(stagedPackageRepository));
            _stagingGroupRepository = stagingGroupRepository ?? throw new ArgumentNullException(nameof(stagingGroupRepository));
            _stagingBlobService = stagingBlobService ?? throw new ArgumentNullException(nameof(stagingBlobService));
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
                Status = stagedPackage.Status.ToString(),
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
                    if (stagedPackage.Status == StagedPackageStatus.Promoting || group?.ActivePromotionId.HasValue == true)
                    {
                        return;
                    }

                    if (group != null)
                    {
                        group.MutationRevision++;
                    }

                    stagedPackage.Status = StagedPackageStatus.Deleted;
                    stagedPackage.StagedPackageIdentity.Package.Listed = false;
                    await _packageService.UpdatePackageStatusAsync(stagedPackage.StagedPackageIdentity.Package, PackageStatus.Deleted, commitChanges: false);
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

        public async Task<StagingGroupDeletionResult> DeleteStagingGroupAsync(User stagingOwner, StagingGroup group)
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

            StagingGroupDeletionResult result = null;
            try
            {
                await _stagingGroupRepository.ExecuteInTransactionAsync(async () =>
                {
                    var groupedAttempts = _stagedPackageRepository
                        .GetAll()
                        .Include(package => package.StagedPackageIdentity.Package)
                        .Where(package => package.StagedPackageIdentity.OwnerKey == stagingOwner.Key)
                        .Where(package => package.StagedPackageIdentity.StagingGroupKey == group.Key)
                        .Where(package => package.StagedPackageIdentity.CurrentStagedPackageKey == package.Key)
                        .ToList();
                    var stagedPackages = groupedAttempts
                        .Where(package => package.StagedPackageIdentity.Package.PackageStatusKey == PackageStatus.Staged)
                        .Where(package => package.Status != StagedPackageStatus.Superseded && package.Status != StagedPackageStatus.Deleted)
                        .ToList();

                    if (group.ActivePromotionId.HasValue)
                    {
                        result = StagingGroupDeletionResult.Conflict(stagedPackages.Count);
                        return;
                    }

                    var stagedPackageKeys = new HashSet<int>(stagedPackages.Select(package => package.Key));
                    foreach (var stagedPackage in groupedAttempts)
                    {
                        var identity = stagedPackage.StagedPackageIdentity;
                        identity.StagingGroupKey = null;
                        identity.StagingGroup = null;

                        if (stagedPackageKeys.Contains(stagedPackage.Key))
                        {
                            var package = identity.Package;
                            stagedPackage.Status = StagedPackageStatus.Deleted;
                            package.Listed = false;
                            await _packageService.UpdatePackageStatusAsync(package, PackageStatus.Deleted, commitChanges: false);
                        }
                    }

                    _stagingGroupRepository.DeleteOnCommit(group);
                    await _stagingGroupRepository.CommitChangesAsync();
                    result = StagingGroupDeletionResult.Deleted(stagedPackages.Count);
                });
            }
            catch (DbUpdateConcurrencyException exception)
            {
                exception.Log();
                result = StagingGroupDeletionResult.Conflict(result?.AffectedPackageCount ?? 0);
            }

            return result;
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

            var identity = stagedPackage.StagedPackageIdentity;
            if (group.OwnerKey != stagingOwner.Key || identity.OwnerKey != stagingOwner.Key)
            {
                throw new ArgumentException("The staging group and package must belong to the authorized owner.");
            }

            if (identity.StagingGroupKey == group.Key)
            {
                return StagingGroupMembershipResult.Unchanged;
            }

            if (stagedPackage.Status == StagedPackageStatus.Promoting || group.ActivePromotionId.HasValue || identity.StagingGroup?.ActivePromotionId.HasValue == true)
            {
                return StagingGroupMembershipResult.Conflict;
            }

            var result = StagingGroupMembershipResult.Conflict;
            try
            {
                await _stagedPackageRepository.ExecuteInTransactionAsync(async () =>
                {
                    stagedPackage.MutationRevision++;
                    if (identity.StagingGroup != null)
                    {
                        identity.StagingGroup.MutationRevision++;
                    }

                    group.MutationRevision++;
                    identity.StagingGroupKey = group.Key;
                    identity.StagingGroup = group;
                    await _stagedPackageRepository.CommitChangesAsync();
                    result = StagingGroupMembershipResult.Updated;
                });
            }
            catch (DbUpdateConcurrencyException exception)
            {
                exception.Log();
            }

            return result;
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

            var identity = stagedPackage.StagedPackageIdentity;
            if (identity.OwnerKey != stagingOwner.Key)
            {
                throw new ArgumentException("The staged package must belong to the authorized owner.");
            }

            if (!identity.StagingGroupKey.HasValue)
            {
                return StagingGroupMembershipResult.Unchanged;
            }

            if (stagedPackage.Status == StagedPackageStatus.Promoting
                || identity.StagingGroup?.ActivePromotionId.HasValue == true)
            {
                return StagingGroupMembershipResult.Conflict;
            }

            var result = StagingGroupMembershipResult.Conflict;
            try
            {
                await _stagedPackageRepository.ExecuteInTransactionAsync(async () =>
                {
                    stagedPackage.MutationRevision++;
                    identity.StagingGroup.MutationRevision++;

                    identity.StagingGroupKey = null;
                    identity.StagingGroup = null;
                    await _stagedPackageRepository.CommitChangesAsync();
                    result = StagingGroupMembershipResult.Updated;
                });
            }
            catch (DbUpdateConcurrencyException exception)
            {
                exception.Log();
            }

            return result;
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

            return groups
                .Select(group => new StagingGroupSummary(group, packagesByGroup[group.Key].ToList()))
                .ToList();
        }

        public StagingGroupSummaryPage GetStagingGroupSummaryPage(User stagingOwner, int page, int pageSize)
        {
            if (stagingOwner == null)
            {
                throw new ArgumentNullException(nameof(stagingOwner));
            }

            if (page <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(page));
            }

            if (pageSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(pageSize));
            }

            var groupsQuery = _stagingGroupRepository
                .GetAll()
                .Include(group => group.Owner)
                .Where(group => group.OwnerKey == stagingOwner.Key);

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

            var summaries = groups
                .Select(group => new StagingGroupSummary(group, packagesByGroup[group.Key].ToList()))
                .ToList();

            return new StagingGroupSummaryPage(summaries, totalCount);
        }

        public StagingGroupPackagePage GetStagingGroupPackagePage(User stagingOwner, string groupId, int page, int pageSize)
        {
            if (stagingOwner == null)
            {
                throw new ArgumentNullException(nameof(stagingOwner));
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

            var group = FindStagingGroup(stagingOwner, groupId);
            if (group == null)
            {
                return null;
            }

            var packagesQuery = GetCurrentStagedPackages(new[] { stagingOwner.Key })
                .Where(package => package.StagedPackageIdentity.StagingGroupKey == group.Key);
            var totalCount = packagesQuery.Count();
            var allPackagesReady = !packagesQuery.Any(package => package.Status != StagedPackageStatus.Ready);
            var skip = ((long)page - 1) * pageSize;

            var packages = new List<StagedPackage>();
            if (skip < totalCount)
            {
                packages = packagesQuery
                    .OrderByDescending(package => package.UploadedDate)
                    .ThenByDescending(package => package.Key)
                    .Skip((int)skip)
                    .Take(pageSize)
                    .ToList();
            }

            return new StagingGroupPackagePage(group, packages, totalCount, allPackagesReady);
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
                .Include(stagedPackage => stagedPackage.StagedPackageIdentity.Package.PackageRegistration)
                .Include(stagedPackage => stagedPackage.StagedPackageIdentity.Owner)
                .Include(stagedPackage => stagedPackage.StagedPackageIdentity.StagingGroup)
                .Where(stagedPackage => ownerKeys.Contains(stagedPackage.StagedPackageIdentity.OwnerKey))
                .Where(stagedPackage => stagedPackage.StagedPackageIdentity.Package.PackageStatusKey == PackageStatus.Staged)
                .Where(stagedPackage => stagedPackage.Status != StagedPackageStatus.Superseded && stagedPackage.Status != StagedPackageStatus.Deleted)
                .Where(stagedPackage => stagedPackage.StagedPackageIdentity.CurrentStagedPackageKey == stagedPackage.Key);
        }

        private StagedPackage GetCurrentAttempt(int packageKey)
        {
            return _stagedPackageRepository
                .GetAll()
                .Include(stagedPackage => stagedPackage.StagedPackageIdentity.Owner)
                .Include(stagedPackage => stagedPackage.StagedPackageIdentity.StagingGroup)
                .SingleOrDefault(stagedPackage => stagedPackage.StagedPackageIdentityKey == packageKey && stagedPackage.StagedPackageIdentity.CurrentStagedPackageKey == stagedPackage.Key);
        }

    }
}
