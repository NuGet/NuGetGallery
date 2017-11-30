// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Security.Principal;

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
        public PermissionsFailure IsAllowed(User currentUser, User account)
        {
            return PermissionsHelpers.IsRequirementSatisfied(AccountPermissionsRequirement, currentUser, account) ? 
                PermissionsFailure.None : PermissionsFailure.Account;
        }

        /// <summary>
        /// Determines whether <paramref name="currentPrincipal"/> can perform this action on <paramref name="account"/>.
        /// </summary>
        public PermissionsFailure IsAllowed(IPrincipal currentPrincipal, User account)
        {
            return PermissionsHelpers.IsRequirementSatisfied(AccountPermissionsRequirement, currentPrincipal, account) ? 
                PermissionsFailure.None : PermissionsFailure.Account;
        }
    }
}