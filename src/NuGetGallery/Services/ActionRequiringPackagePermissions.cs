﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;

namespace NuGetGallery
{
    /// <summary>
    /// An action requiring permissions on a <see cref="PackageRegistration"/> or <see cref="Package"/> that can be done on behalf of another <see cref="User"/>.
    /// </summary>
    public class ActionRequiringPackagePermissions
        : ActionRequiringEntityPermissions<PackageRegistration>, IActionRequiringEntityPermissions<Package>
    {
        public PermissionsRequirement PackageRegistrationPermissionsRequirement { get; }

        public ActionRequiringPackagePermissions(
            PermissionsRequirement accountOnBehalfOfPermissionsRequirement,
            PermissionsRequirement packageRegistrationPermissionsRequirement)
            : base(accountOnBehalfOfPermissionsRequirement)
        {
            PackageRegistrationPermissionsRequirement = packageRegistrationPermissionsRequirement;
        }

        public PermissionsFailure IsAllowed(User currentUser, User account, Package package)
        {
            return IsAllowed(currentUser, account, ConvertPackageToRegistration(package));
        }

        public PermissionsFailure IsAllowed(IPrincipal currentPrincipal, User account, Package package)
        {
            return IsAllowed(currentPrincipal, account, ConvertPackageToRegistration(package));
        }

        protected override PermissionsFailure IsAllowedOnEntity(User account, PackageRegistration packageRegistration)
        {
            return PermissionsHelpers.IsRequirementSatisfied(PackageRegistrationPermissionsRequirement, account, packageRegistration) ? PermissionsFailure.None : PermissionsFailure.PackageRegistration;
        }

        public bool TryGetAccountsIsAllowedOnBehalfOf(User currentUser, Package package, out IEnumerable<User> accountsAllowedOnBehalfOf)
        {
            return TryGetAccountsIsAllowedOnBehalfOf(currentUser, ConvertPackageToRegistration(package), out accountsAllowedOnBehalfOf);
        }

        protected override IEnumerable<User> GetOwners(PackageRegistration packageRegistration)
        {
            return packageRegistration != null ? packageRegistration.Owners : Enumerable.Empty<User>();
        }

        private PackageRegistration ConvertPackageToRegistration(Package package)
        {
            return package.PackageRegistration;
        }
    }
}