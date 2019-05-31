// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    /// <summary>
    /// An action requiring permissions on <see cref="ReservedNamespace"/>s that can be done on behalf of another <see cref="User"/>.
    /// </summary>
    /// <remarks>
    /// These permissions refer to <see cref="IReadOnlyCollection{ReservedNamespace}"/> and not a single <see cref="ReservedNamespace"/> because multiple namespaces can apply to a single ID.
    /// E.g. "JQuery.Extensions.MyCoolExtension" matches both "JQuery.*" and "JQuery.Extensions.*".
    /// </remarks>
    public class ActionRequiringReservedNamespacePermissions
        : ActionRequiringEntityPermissions<IReadOnlyCollection<ReservedNamespace>>, 
        IActionRequiringEntityPermissions<ActionOnNewPackageContext>, 
        IActionRequiringEntityPermissions<ReservedNamespace>
    {
        public PermissionsRequirement ReservedNamespacePermissionsRequirement { get; }

        public ActionRequiringReservedNamespacePermissions(
            PermissionsRequirement accountOnBehalfOfPermissionsRequirement,
            PermissionsRequirement reservedNamespacePermissionsRequirement)
            : base(accountOnBehalfOfPermissionsRequirement)
        {
            ReservedNamespacePermissionsRequirement = reservedNamespacePermissionsRequirement;
        }

        public PermissionsCheckResult CheckPermissions(User currentUser, User account, ActionOnNewPackageContext newPackageContext)
        {
            return CheckPermissions(currentUser, account, GetReservedNamespaces(newPackageContext));
        }

        public PermissionsCheckResult CheckPermissions(User currentUser, User account, ReservedNamespace reservedNamespace)
        {
            return CheckPermissions(currentUser, account, GetReservedNamespaces(reservedNamespace));
        }

        public PermissionsCheckResult CheckPermissions(IPrincipal currentPrincipal, User account, ActionOnNewPackageContext newPackageContext)
        {
            return CheckPermissions(currentPrincipal, account, GetReservedNamespaces(newPackageContext));
        }

        public PermissionsCheckResult CheckPermissions(IPrincipal currentPrincipal, User account, ReservedNamespace reservedNamespace)
        {
            return CheckPermissions(currentPrincipal, account, GetReservedNamespaces(reservedNamespace));
        }

        protected override PermissionsCheckResult CheckPermissionsForEntity(User account, IReadOnlyCollection<ReservedNamespace> reservedNamespaces)
        {
            if (!reservedNamespaces.Any())
            {
                return PermissionsCheckResult.Allowed;
            }

            // Permissions on only a single namespace are required to perform the action.
            return reservedNamespaces.Any(rn => PermissionsHelpers.IsRequirementSatisfied(ReservedNamespacePermissionsRequirement, account, rn)) ?
                PermissionsCheckResult.Allowed : PermissionsCheckResult.ReservedNamespaceFailure;
        }

        public PermissionsCheckResult CheckPermissionsOnBehalfOfAnyAccount(User currentUser, ActionOnNewPackageContext newPackageContext)
        {
            return CheckPermissionsOnBehalfOfAnyAccount(currentUser, GetReservedNamespaces(newPackageContext));
        }

        public PermissionsCheckResult CheckPermissionsOnBehalfOfAnyAccount(User currentUser, ReservedNamespace reservedNamespace)
        {
            return CheckPermissionsOnBehalfOfAnyAccount(currentUser, GetReservedNamespaces(reservedNamespace));
        }

        public PermissionsCheckResult CheckPermissionsOnBehalfOfAnyAccount(User currentUser, ActionOnNewPackageContext newPackageContext, out IEnumerable<User> accountsAllowedOnBehalfOf)
        {
            return CheckPermissionsOnBehalfOfAnyAccount(currentUser, GetReservedNamespaces(newPackageContext), out accountsAllowedOnBehalfOf);
        }

        public PermissionsCheckResult CheckPermissionsOnBehalfOfAnyAccount(User currentUser, ReservedNamespace reservedNamespace, out IEnumerable<User> accountsAllowedOnBehalfOf)
        {
            return CheckPermissionsOnBehalfOfAnyAccount(currentUser, GetReservedNamespaces(reservedNamespace), out accountsAllowedOnBehalfOf);
        }

        protected override IEnumerable<User> GetOwners(IReadOnlyCollection<ReservedNamespace> reservedNamespaces)
        {
            return reservedNamespaces.Any() ? reservedNamespaces.SelectMany(rn => rn.Owners) : Enumerable.Empty<User>();
        }

        private IReadOnlyCollection<ReservedNamespace> GetReservedNamespaces(ActionOnNewPackageContext newPackageContext)
        {
            return newPackageContext.ReservedNamespaceService.GetReservedNamespacesForId(newPackageContext.PackageId);
        }

        private IReadOnlyCollection<ReservedNamespace> GetReservedNamespaces(ReservedNamespace reservedNamespace)
        {
            return new[] { reservedNamespace };
        }
    }
}