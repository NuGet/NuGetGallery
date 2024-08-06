// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Security.Principal;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    /// <summary>
    /// An action requiring permissions on a <see cref="User"/>.
    /// </summary>
    public class ActionRequiringAccountPermissions
    {
        public PermissionsRequirement AccountPermissionsRequirement { get; }
        
        public ActionRequiringAccountPermissions(PermissionsRequirement accountPermissionsRequirement)
        {
            AccountPermissionsRequirement = accountPermissionsRequirement;
        }

        /// <summary>
        /// Determines whether <paramref name="currentUser"/> can perform this action on <paramref name="account"/>.
        /// </summary>
        public PermissionsCheckResult CheckPermissions(User currentUser, User account)
        {
            return PermissionsHelpers.IsRequirementSatisfied(AccountPermissionsRequirement, currentUser, account) ? 
                PermissionsCheckResult.Allowed : PermissionsCheckResult.AccountFailure;
        }

        /// <summary>
        /// Determines whether <paramref name="currentPrincipal"/> can perform this action on <paramref name="account"/>.
        /// </summary>
        public PermissionsCheckResult CheckPermissions(IPrincipal currentPrincipal, User account)
        {
            return PermissionsHelpers.IsRequirementSatisfied(AccountPermissionsRequirement, currentPrincipal, account) ? 
                PermissionsCheckResult.Allowed : PermissionsCheckResult.AccountFailure;
        }
    }
}