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

        public async Task UpdateListedAsync(StagedPackage stagedPackage, bool listed)
        {
            if (stagedPackage == null)
            {
                throw new ArgumentNullException(nameof(stagedPackage));
            }

            stagedPackage.StagedPackageIdentity.Package.Listed = listed;
            await _stagedPackageRepository.CommitChangesAsync();
        }

        public async Task DeletePackageAsync(StagedPackage stagedPackage)
        {
            if (stagedPackage == null)
            {
                throw new ArgumentNullException(nameof(stagedPackage));
            }

            stagedPackage.Status = StagedPackageStatus.Deleted;
            stagedPackage.StagedPackageIdentity.Package.Listed = false;
            await _packageService.UpdatePackageStatusAsync(stagedPackage.StagedPackageIdentity.Package, PackageStatus.Deleted, commitChanges: false);
            await _stagedPackageRepository.CommitChangesAsync();
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

            var groupExists = _stagingGroupRepository
                .GetAll()
                .Where(group => group.OwnerKey == stagingOwner.Key)
                .Select(group => group.Id)
                .ToList()
                .Any(id => string.Equals(id, groupId, StringComparison.OrdinalIgnoreCase));
            if (groupExists)
            {
                return CreateStagingGroupResult.GroupAlreadyExists();
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
            if (group == null)
            {
                return null;
            }

            group.Name = name.Trim();
            await _stagingGroupRepository.CommitChangesAsync();
            return group;
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
                .Where(stagedPackage => _packageStagingAuthorizationService.CanManageWithApiKey(currentUser, scopes, stagedPackage))
                .Select(GetStatus)
                .OrderBy(package => package.Id)
                .ThenBy(package => package.Version)
                .ToList();
        }

        private IEnumerable<StagedPackage> GetCurrentStagedPackages(int[] ownerKeys)
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
                .SingleOrDefault(stagedPackage => stagedPackage.StagedPackageIdentityKey == packageKey && stagedPackage.StagedPackageIdentity.CurrentStagedPackageKey == stagedPackage.Key);
        }

    }
}
