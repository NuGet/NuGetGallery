// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using NuGet.Services.Entities;

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

        public PermissionsCheckResult CheckPermissions(User currentUser, User account, Package package)
        {
            return CheckPermissions(currentUser, account, GetPackageRegistration(package));
        }

        public PermissionsCheckResult CheckPermissions(IPrincipal currentPrincipal, User account, Package package)
        {
            return CheckPermissions(currentPrincipal, account, GetPackageRegistration(package));
        }

        protected override PermissionsCheckResult CheckPermissionsForEntity(User account, PackageRegistration packageRegistration)
        {
            return PermissionsHelpers.IsRequirementSatisfied(PackageRegistrationPermissionsRequirement, account, packageRegistration) ? PermissionsCheckResult.Allowed : PermissionsCheckResult.PackageRegistrationFailure;
        }

        public PermissionsCheckResult CheckPermissionsOnBehalfOfAnyAccount(User currentUser, Package package)
        {
            return CheckPermissionsOnBehalfOfAnyAccount(currentUser, GetPackageRegistration(package));
        }

        public PermissionsCheckResult CheckPermissionsOnBehalfOfAnyAccount(User currentUser, Package package, out IEnumerable<User> accountsAllowedOnBehalfOf)
        {
            return CheckPermissionsOnBehalfOfAnyAccount(currentUser, GetPackageRegistration(package), out accountsAllowedOnBehalfOf);
        }

        protected override IEnumerable<User> GetOwners(PackageRegistration packageRegistration)
        {
            return packageRegistration != null ? packageRegistration.Owners : Enumerable.Empty<User>();
        }

        private PackageRegistration GetPackageRegistration(Package package)
        {
            return package.PackageRegistration;
        }
    }
}