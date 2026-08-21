// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using NuGet.Services.Entities;
using NuGetGallery.Authentication;

namespace NuGetGallery
{
    public class PackageStagingManagementService : IPackageStagingManagementService
    {
        private readonly IEntitiesContext _entitiesContext;
        private readonly IApiScopeEvaluator _apiScopeEvaluator;
        private readonly IFeatureFlagService _featureFlagService;
        private readonly IPackageService _packageService;

        public PackageStagingManagementService(
            IEntitiesContext entitiesContext,
            IApiScopeEvaluator apiScopeEvaluator,
            IFeatureFlagService featureFlagService,
            IPackageService packageService)
        {
            _entitiesContext = entitiesContext ?? throw new ArgumentNullException(nameof(entitiesContext));
            _apiScopeEvaluator = apiScopeEvaluator ?? throw new ArgumentNullException(nameof(apiScopeEvaluator));
            _featureFlagService = featureFlagService ?? throw new ArgumentNullException(nameof(featureFlagService));
            _packageService = packageService ?? throw new ArgumentNullException(nameof(packageService));
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

            var stagedPackage = _entitiesContext.StagedPackages.Find(package.Key);
            if (stagedPackage == null)
            {
                return null;
            }

            var authorizationResult = _apiScopeEvaluator.Evaluate(
                currentUser,
                scopes,
                ActionsRequiringPermissions.UploadNewPackageVersion,
                package.PackageRegistration,
                NuGetScopes.PackagePushVersion,
                NuGetScopes.PackagePush);
            var owner = authorizationResult.Owner;
            if (!authorizationResult.IsSuccessful() || stagedPackage.OwnerKey != owner.Key || !_featureFlagService.IsPackageStagingEnabled(owner))
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

        public IReadOnlyList<StagedPackage> GetStagedPackages(User currentUser)
        {
            if (currentUser == null)
            {
                throw new ArgumentNullException(nameof(currentUser));
            }

            var ownerKeys = GetEnabledOwners(currentUser)
                .Select(owner => owner.Key)
                .ToArray();

            IQueryable<StagedPackage> stagedPackages = _entitiesContext.StagedPackages;

            return stagedPackages
                .Include(stagedPackage => stagedPackage.Package.PackageRegistration)
                .Include(stagedPackage => stagedPackage.Owner)
                .Where(stagedPackage => ownerKeys.Contains(stagedPackage.OwnerKey))
                .OrderBy(stagedPackage => stagedPackage.Package.PackageRegistration.Id)
                .ThenByDescending(stagedPackage => stagedPackage.UploadedDate)
                .ToList();
        }

        private IEnumerable<User> GetEnabledOwners(User currentUser)
        {
            return new[] { currentUser }
                .Concat(currentUser.Organizations.Select(membership => membership.Organization))
                .Where(owner => _featureFlagService.IsPackageStagingEnabled(owner));
        }
    }
}
