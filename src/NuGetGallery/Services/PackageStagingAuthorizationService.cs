// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Services.Entities;
using NuGetGallery.Authentication;

namespace NuGetGallery
{
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

            var permissionsResult = ActionsRequiringPermissions.UploadNewPackageVersion.CheckPermissions(
                currentUser,
                stagedPackage.Owner,
                stagedPackage.Package.PackageRegistration);

            return permissionsResult == PermissionsCheckResult.Allowed
                && _featureFlagService.IsPackageStagingEnabled(stagedPackage.Owner);
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
                ActionsRequiringPermissions.UploadNewPackageVersion,
                stagedPackage.Package.PackageRegistration,
                NuGetScopes.PackagePushVersion,
                NuGetScopes.PackagePush);

            return authorizationResult.IsSuccessful()
                && authorizationResult.Owner.Key == stagedPackage.OwnerKey
                && _featureFlagService.IsPackageStagingEnabled(authorizationResult.Owner);
        }
    }
}
