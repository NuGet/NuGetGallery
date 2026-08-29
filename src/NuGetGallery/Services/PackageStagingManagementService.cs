// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public class PackageStagingManagementService : IPackageStagingManagementService
    {
        private readonly IPackageStagingAuthorizationService _packageStagingAuthorizationService;
        private readonly IFeatureFlagService _featureFlagService;
        private readonly IPackageService _packageService;
        private readonly IEntityRepository<StagedPackage> _stagedPackageRepository;
        private readonly IStagingBlobService _stagingBlobService;

        public PackageStagingManagementService(
            IPackageStagingAuthorizationService packageStagingAuthorizationService,
            IFeatureFlagService featureFlagService,
            IPackageService packageService,
            IEntityRepository<StagedPackage> stagedPackageRepository,
            IStagingBlobService stagingBlobService)
        {
            _packageStagingAuthorizationService = packageStagingAuthorizationService ?? throw new ArgumentNullException(nameof(packageStagingAuthorizationService));
            _featureFlagService = featureFlagService ?? throw new ArgumentNullException(nameof(featureFlagService));
            _packageService = packageService ?? throw new ArgumentNullException(nameof(packageService));
            _stagedPackageRepository = stagedPackageRepository ?? throw new ArgumentNullException(nameof(stagedPackageRepository));
            _stagingBlobService = stagingBlobService ?? throw new ArgumentNullException(nameof(stagingBlobService));
        }

        public PackageStagingStatus GetPackage(User currentUser, IEnumerable<Scope> scopes, string id, string version)
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

            var package = _packageService.FindPackageByIdAndVersionStrict(id, version);
            if (package == null)
            {
                return null;
            }

            var stagedPackage = _stagedPackageRepository
                .GetAll()
                .Where(candidate => candidate.PackageKey == package.Key)
                .OrderByDescending(candidate => candidate.Key)
                .FirstOrDefault();
            if (stagedPackage == null)
            {
                return null;
            }

            if (!_packageStagingAuthorizationService.CanManageWithApiKey(currentUser, scopes, stagedPackage))
            {
                return null;
            }

            return new PackageStagingStatus
            {
                Id = package.PackageRegistration.Id,
                Version = package.NormalizedVersion,
                Status = PackageStatus.Staged.ToString(),
            };
        }

        public bool IsEnabled(User currentUser)
        {
            if (currentUser == null)
            {
                throw new ArgumentNullException(nameof(currentUser));
            }

            return GetEnabledOwners(currentUser).Any();
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

        public IReadOnlyList<StagedPackage> GetStagedPackages(User currentUser)
        {
            if (currentUser == null)
            {
                throw new ArgumentNullException(nameof(currentUser));
            }

            var ownerKeys = GetEnabledOwners(currentUser)
                .Select(owner => owner.Key)
                .ToArray();

            var stagedPackages = _stagedPackageRepository.GetAll();

            return stagedPackages
                .Include(stagedPackage => stagedPackage.Package.PackageRegistration)
                .Include(stagedPackage => stagedPackage.Owner)
                .Where(stagedPackage => ownerKeys.Contains(stagedPackage.OwnerKey))
                .Where(stagedPackage => stagedPackage.Package.PackageStatusKey == PackageStatus.Staged)
                .AsEnumerable()
                .GroupBy(stagedPackage => stagedPackage.PackageKey)
                .Select(attempts => attempts.OrderByDescending(attempt => attempt.Key).First())
                .Where(stagedPackage => _packageStagingAuthorizationService.CanManage(currentUser, stagedPackage))
                .OrderBy(stagedPackage => stagedPackage.Package.PackageRegistration.Id)
                .ThenByDescending(stagedPackage => stagedPackage.UploadedDate)
                .ToList();
        }

        private StagedPackage GetCurrentAttempt(int packageKey)
        {
            return _stagedPackageRepository
                .GetAll()
                .Include(stagedPackage => stagedPackage.Owner)
                .Where(stagedPackage => stagedPackage.PackageKey == packageKey)
                .OrderByDescending(stagedPackage => stagedPackage.Key)
                .FirstOrDefault();
        }

        private IEnumerable<User> GetEnabledOwners(User currentUser)
        {
            return new[] { currentUser }
                .Concat(currentUser.Organizations.Select(membership => membership.Organization))
                .Where(owner => _featureFlagService.IsPackageStagingEnabled(owner));
        }
    }
}
