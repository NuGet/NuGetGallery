// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    /// <summary>
    /// An action requiring ownership of both a staged package attempt and its package registration.
    /// </summary>
    public class ActionRequiringStagedPackagePermissions : ActionRequiringEntityPermissions<StagedPackage>
    {
        public ActionRequiringStagedPackagePermissions(
            PermissionsRequirement accountOnBehalfOfPermissionsRequirement)
            : base(accountOnBehalfOfPermissionsRequirement)
        {
        }

        protected override PermissionsCheckResult CheckPermissionsForEntity(User account, StagedPackage stagedPackage)
        {
            if (account.Key != stagedPackage.OwnerKey)
            {
                return PermissionsCheckResult.StagedPackageFailure;
            }

            return PermissionsHelpers.IsRequirementSatisfied(
                PermissionsRequirement.Owner,
                account,
                stagedPackage.Package.PackageRegistration)
                    ? PermissionsCheckResult.Allowed
                    : PermissionsCheckResult.PackageRegistrationFailure;
        }

        protected override IEnumerable<User> GetOwners(StagedPackage stagedPackage)
        {
            return stagedPackage?.Owner != null
                ? new[] { stagedPackage.Owner }
                : Enumerable.Empty<User>();
        }
    }
}
