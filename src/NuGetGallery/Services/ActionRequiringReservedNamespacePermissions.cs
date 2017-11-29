// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;

namespace NuGetGallery
{
    /// <summary>
    /// An action requiring permissions on a <see cref="Package"/>.
    /// </summary>
    public class ActionRequiringReservedNamespacePermissions
        : ActionRequiringEntityPermissions<IEnumerable<ReservedNamespace>>
    {
        public ActionRequiringReservedNamespacePermissions(
            PermissionsRequirement accountOnBehalfOfPermissionsRequirement,
            PermissionsRequirement reservedNamespacePermissionsRequirement)
            : base(accountOnBehalfOfPermissionsRequirement, reservedNamespacePermissionsRequirement)
        {
        }

        public PermissionsFailure IsAllowed(User currentUser, User account, string packageId, IReservedNamespaceService reservedNamespaceService)
        {
            return IsAllowed(currentUser, account, reservedNamespaceService.GetReservedNamespacesForId(packageId));
        }

        protected override PermissionsFailure IsAllowedOnEntity(User account, IEnumerable<ReservedNamespace> reservedNamespaces)
        {
            if (!reservedNamespaces.Any())
            {
                return PermissionsFailure.None;
            }

            return reservedNamespaces.Any(rn => PermissionsHelpers.IsRequirementSatisfied(EntityPermissionsRequirement, account, rn)) ?
                PermissionsFailure.None : PermissionsFailure.ReservedNamespace;
        }

        public bool TryGetAccountsIsAllowedOnBehalfOf(User currentUser, string packageId, IReservedNamespaceService reservedNamespaceService, out IEnumerable<User> accountsAllowedOnBehalfOf)
        {
            return TryGetAccountsIsAllowedOnBehalfOf(currentUser, reservedNamespaceService.GetReservedNamespacesForId(packageId), out accountsAllowedOnBehalfOf);
        }

        protected override IEnumerable<User> GetOwners(IEnumerable<ReservedNamespace> reservedNamespaces)
        {
            return reservedNamespaces.Any() ? reservedNamespaces.SelectMany(rn => rn.Owners) : Enumerable.Empty<User>();
        }
    }
}