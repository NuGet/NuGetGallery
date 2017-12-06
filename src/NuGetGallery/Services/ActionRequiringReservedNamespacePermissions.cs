﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;

namespace NuGetGallery
{
    /// <summary>
    /// Context object for checking permissions of an action involving a new package ID.
    /// </summary>
    public class ActionOnNewPackageContext
    {
        public string PackageId { get; }
        public IReservedNamespaceService ReservedNamespaceService { get; }

        public ActionOnNewPackageContext(string packageId, IReservedNamespaceService reservedNamespaceService)
        {
            PackageId = packageId;
            ReservedNamespaceService = reservedNamespaceService;
        }
    }

    /// <summary>
    /// An action requiring permissions on a <see cref="ReservedNamespace"/> that can be done on behalf of another <see cref="User"/>.
    /// </summary>
    public class ActionRequiringReservedNamespacePermissions
        : ActionRequiringEntityPermissions<IEnumerable<ReservedNamespace>>, IActionRequiringEntityPermissions<ActionOnNewPackageContext>
    {
        public PermissionsRequirement ReservedNamespacePermissionsRequirement { get; }

        public ActionRequiringReservedNamespacePermissions(
            PermissionsRequirement accountOnBehalfOfPermissionsRequirement,
            PermissionsRequirement reservedNamespacePermissionsRequirement)
            : base(accountOnBehalfOfPermissionsRequirement)
        {
            ReservedNamespacePermissionsRequirement = reservedNamespacePermissionsRequirement;
        }

        public PermissionsFailure IsAllowed(User currentUser, User account, ActionOnNewPackageContext newPackageContext)
        {
            return IsAllowed(currentUser, account, ConvertActionOnNewPackageContextToReservedNamespace(newPackageContext));
        }

        public PermissionsFailure IsAllowed(IPrincipal currentPrincipal, User account, ActionOnNewPackageContext newPackageContext)
        {
            return IsAllowed(currentPrincipal, account, ConvertActionOnNewPackageContextToReservedNamespace(newPackageContext));
        }

        protected override PermissionsFailure IsAllowedOnEntity(User account, IEnumerable<ReservedNamespace> reservedNamespaces)
        {
            if (!reservedNamespaces.Any())
            {
                return PermissionsFailure.None;
            }

            return reservedNamespaces.Any(rn => PermissionsHelpers.IsRequirementSatisfied(ReservedNamespacePermissionsRequirement, account, rn)) ?
                PermissionsFailure.None : PermissionsFailure.ReservedNamespace;
        }

        public bool TryGetAccountsIsAllowedOnBehalfOf(User currentUser, ActionOnNewPackageContext newPackageContext, out IEnumerable<User> accountsAllowedOnBehalfOf)
        {
            return TryGetAccountsIsAllowedOnBehalfOf(currentUser, ConvertActionOnNewPackageContextToReservedNamespace(newPackageContext), out accountsAllowedOnBehalfOf);
        }

        protected override IEnumerable<User> GetOwners(IEnumerable<ReservedNamespace> reservedNamespaces)
        {
            return reservedNamespaces.Any() ? reservedNamespaces.SelectMany(rn => rn.Owners) : Enumerable.Empty<User>();
        }

        private IEnumerable<ReservedNamespace> ConvertActionOnNewPackageContextToReservedNamespace(ActionOnNewPackageContext newPackageContext)
        {
            return newPackageContext.ReservedNamespaceService.GetReservedNamespacesForId(newPackageContext.PackageId);
        }
    }
}