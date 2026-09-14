// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Services.Entities;
using NuGetGallery.Authentication;

namespace NuGetGallery
{
    /// <summary>
    /// Authorizes staged-package management using ownership, feature flags, and API scopes.
    /// </summary>
    public class PackageStagingAuthorizationService : IPackageStagingAuthorizationService
    {
        private readonly IApiScopeEvaluator _apiScopeEvaluator;
        private readonly IFeatureFlagService _featureFlagService;

        public PackageStagingAuthorizationService(
            IApiScopeEvaluator apiScopeEvaluator,
            IFeatureFlagService featureFlagService)
        {
            _apiScopeEvaluator = apiScopeEvaluator ?? throw new ArgumentNullException(nameof(apiScopeEvaluator));
            _featureFlagService = featureFlagService ?? throw new ArgumentNullException(nameof(featureFlagService));
        }

        public IReadOnlyList<User> GetEnabledOwners(User currentUser)
        {
            if (currentUser == null)
            {
                throw new ArgumentNullException(nameof(currentUser));
            }

            return new[] { currentUser }
                .Concat(currentUser.Organizations.Select(membership => membership.Organization))
                .Where(owner => _featureFlagService.IsPackageStagingEnabled(owner))
                .OrderBy(owner => owner.Username)
                .ToList();
        }

        public User GetEnabledOwner(User currentUser, string owner)
        {
            if (string.IsNullOrWhiteSpace(owner))
            {
                throw new ArgumentException(CoreStrings.PackageIsMissingRequiredData, nameof(owner));
            }

            return GetEnabledOwners(currentUser)
                .SingleOrDefault(candidate => string.Equals(candidate.Username, owner, StringComparison.OrdinalIgnoreCase));
        }

        public bool CanManage(User currentUser, StagedPackage stagedPackage)
        {
            if (currentUser == null)
            {
                throw new ArgumentNullException(nameof(currentUser));
            }

            if (stagedPackage == null)
            {
                throw new ArgumentNullException(nameof(stagedPackage));
            }

            var permissionsResult = ActionsRequiringPermissions.ManageStagedPackage.CheckPermissions(
                currentUser,
                stagedPackage.StagedPackageIdentity.Owner,
                stagedPackage);

            return permissionsResult == PermissionsCheckResult.Allowed
                && _featureFlagService.IsPackageStagingEnabled(stagedPackage.StagedPackageIdentity.Owner);
        }

        public bool CanManageWithApiKey(User currentUser, IEnumerable<Scope> scopes, StagedPackage stagedPackage)
        {
            if (currentUser == null)
            {
                throw new ArgumentNullException(nameof(currentUser));
            }

            if (stagedPackage == null)
            {
                throw new ArgumentNullException(nameof(stagedPackage));
            }

            var authorizationResult = _apiScopeEvaluator.Evaluate(
                currentUser,
                scopes,
                ActionsRequiringPermissions.ManageStagedPackage,
                stagedPackage,
                NuGetScopes.PackagePushVersion,
                NuGetScopes.PackagePush);

            return authorizationResult.IsSuccessful()
                && _featureFlagService.IsPackageStagingEnabled(authorizationResult.Owner);
        }

        public User GetEnabledApiKeyOwner(User currentUser, IEnumerable<Scope> scopes)
        {
            if (currentUser == null)
            {
                throw new ArgumentNullException(nameof(currentUser));
            }

            if (scopes == null)
            {
                throw new ArgumentNullException(nameof(scopes));
            }

            var ownerKeys = scopes
                .Where(scope => scope.OwnerKey.HasValue)
                .Select(scope => scope.OwnerKey.Value)
                .Distinct()
                .ToList();
            if (ownerKeys.Count != 1)
            {
                return null;
            }

            return GetEnabledOwners(currentUser)
                .SingleOrDefault(owner => owner.Key == ownerKeys[0]);
        }
    }
}
