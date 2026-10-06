// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    /// <summary>
    /// An action requiring ownership of a private staged package attempt.
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
            if (account.Key != stagedPackage.StagedPackageIdentity.OwnerKey)
            {
                return PermissionsCheckResult.StagedPackageFailure;
            }

            return PermissionsCheckResult.Allowed;
        }

        protected override IEnumerable<User> GetOwners(StagedPackage stagedPackage)
        {
            return stagedPackage?.StagedPackageIdentity?.Owner != null
                ? new[] { stagedPackage.StagedPackageIdentity.Owner }
                : Enumerable.Empty<User>();
        }
    }
}
