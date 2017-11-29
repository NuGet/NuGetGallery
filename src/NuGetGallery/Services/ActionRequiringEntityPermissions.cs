// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;

namespace NuGetGallery
{
    public abstract class ActionRequiringEntityPermissions<TEntity>
        : IActionRequiringEntityPermissions<TEntity>
    {
        public PermissionsRequirement AccountOnBehalfOfPermissionsRequirement { get; }
        public PermissionsRequirement EntityPermissionsRequirement { get; }

        public ActionRequiringEntityPermissions(
            PermissionsRequirement accountOnBehalfOfPermissionsRequirement,
            PermissionsRequirement entityPermissionsRequirement)
        {
            AccountOnBehalfOfPermissionsRequirement = accountOnBehalfOfPermissionsRequirement;
            EntityPermissionsRequirement = entityPermissionsRequirement;
        }
        
        public PermissionsFailure IsAllowed(User currentUser, User account, TEntity entity)
        {
            if (!PermissionsHelpers.IsRequirementSatisfied(AccountOnBehalfOfPermissionsRequirement, currentUser, account))
            {
                return PermissionsFailure.Account;
            }
            
            return IsAllowedOnEntity(account, entity);
        }
        
        public PermissionsFailure IsAllowed(IPrincipal currentPrincipal, User account, TEntity entity)
        {
            if (!PermissionsHelpers.IsRequirementSatisfied(AccountOnBehalfOfPermissionsRequirement, currentPrincipal, account))
            {
                return PermissionsFailure.Account;
            }

            return IsAllowedOnEntity(account, entity);
        }

        protected abstract PermissionsFailure IsAllowedOnEntity(User account, TEntity entity);

        public bool TryGetAccountsIsAllowedOnBehalfOf(User currentUser, TEntity entity, out IEnumerable<User> accountsAllowedOnBehalfOf)
        {
            accountsAllowedOnBehalfOf = Enumerable.Empty<User>();
            var possibleAccountsOnBehalfOf = new[] { currentUser }.Concat(GetOwners(entity)).Distinct();

            foreach (var accountOnBehalfOf in possibleAccountsOnBehalfOf)
            {
                var failure = IsAllowed(currentUser, accountOnBehalfOf, entity);
                if (failure == PermissionsFailure.None)
                {
                    accountsAllowedOnBehalfOf = accountsAllowedOnBehalfOf.Concat(new[] { accountOnBehalfOf });
                }
            }

            return accountsAllowedOnBehalfOf.Any();
        }

        protected abstract IEnumerable<User> GetOwners(TEntity entity);
    }
}