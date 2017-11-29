// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;

namespace NuGetGallery
{
    /// <summary>
    /// An action requiring permissions on a <see cref="Package"/>.
    /// </summary>
    public class ActionRequiringPackagePermissions
        : ActionRequiringEntityPermissions<PackageRegistration>
    {
        public ActionRequiringPackagePermissions(
            PermissionsRequirement accountOnBehalfOfPermissionsRequirement,
            PermissionsRequirement packageRegistrationPermissionsRequirement)
            : base(accountOnBehalfOfPermissionsRequirement, packageRegistrationPermissionsRequirement)
        {
        }

        public PermissionsFailure IsAllowed(User currentUser, User account, Package package)
        {
            return IsAllowed(currentUser, account, package.PackageRegistration);
        }

        public PermissionsFailure IsAllowed(IPrincipal currentPrincipal, User account, Package package)
        {
            return IsAllowed(currentPrincipal, account, package.PackageRegistration);
        }

        protected override PermissionsFailure IsAllowedOnEntity(User account, PackageRegistration packageRegistration)
        {
            return PermissionsHelpers.IsRequirementSatisfied(EntityPermissionsRequirement, account, packageRegistration) ? PermissionsFailure.None : PermissionsFailure.PackageRegistration;
        }

        public bool TryGetAccountsIsAllowedOnBehalfOf(User currentUser, Package package, out IEnumerable<User> accountsAllowedOnBehalfOf)
        {
            return TryGetAccountsIsAllowedOnBehalfOf(currentUser, package.PackageRegistration, out accountsAllowedOnBehalfOf);
        }

        protected override IEnumerable<User> GetOwners(PackageRegistration packageRegistration)
        {
            return packageRegistration.Owners;
        }
    }
}