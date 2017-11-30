// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Security.Principal;

namespace NuGetGallery
{
    /// <summary>
    /// An action requiring permissions on a <see cref="Package"/> that can be done on behalf of another <see cref="User"/>.
    /// </summary>
    public class ActionRequiringPackagePermissions
        : ActionRequiringEntityPermissions<PackageRegistration>, IActionRequiringEntityPermissions<Package>, IActionRequiringEntityPermissions<ListPackageItemViewModel>
    {
        public ActionRequiringPackagePermissions(
            PermissionsRequirement accountOnBehalfOfPermissionsRequirement,
            PermissionsRequirement packageRegistrationPermissionsRequirement)
            : base(accountOnBehalfOfPermissionsRequirement, packageRegistrationPermissionsRequirement)
        {
        }

        public PermissionsFailure IsAllowed(User currentUser, User account, Package package)
        {
            return IsAllowed(currentUser, account, ConvertPackageToRegistration(package));
        }

        public PermissionsFailure IsAllowed(User currentUser, User account, ListPackageItemViewModel model)
        {
            return IsAllowed(currentUser, account, ConvertListPackageItemViewModelToRegistration(model));
        }

        public PermissionsFailure IsAllowed(IPrincipal currentPrincipal, User account, Package package)
        {
            return IsAllowed(currentPrincipal, account, ConvertPackageToRegistration(package));
        }

        public PermissionsFailure IsAllowed(IPrincipal currentPrincipal, User account, ListPackageItemViewModel model)
        {
            return IsAllowed(currentPrincipal, account, ConvertListPackageItemViewModelToRegistration(model));
        }

        protected override PermissionsFailure IsAllowedOnEntity(User account, PackageRegistration packageRegistration)
        {
            return PermissionsHelpers.IsRequirementSatisfied(EntityPermissionsRequirement, account, packageRegistration) ? PermissionsFailure.None : PermissionsFailure.PackageRegistration;
        }

        public bool TryGetAccountsIsAllowedOnBehalfOf(User currentUser, Package package, out IEnumerable<User> accountsAllowedOnBehalfOf)
        {
            return TryGetAccountsIsAllowedOnBehalfOf(currentUser, ConvertPackageToRegistration(package), out accountsAllowedOnBehalfOf);
        }

        public bool TryGetAccountsIsAllowedOnBehalfOf(User currentUser, ListPackageItemViewModel model, out IEnumerable<User> accountsAllowedOnBehalfOf)
        {
            return TryGetAccountsIsAllowedOnBehalfOf(currentUser, ConvertListPackageItemViewModelToRegistration(model), out accountsAllowedOnBehalfOf);
        }

        protected override IEnumerable<User> GetOwners(PackageRegistration packageRegistration)
        {
            return packageRegistration.Owners;
        }

        private PackageRegistration ConvertPackageToRegistration(Package package)
        {
            return package.PackageRegistration;
        }

        private PackageRegistration ConvertListPackageItemViewModelToRegistration(ListPackageItemViewModel model)
        {
            return new PackageRegistration { Owners = model.Owners };
        }
    }
}