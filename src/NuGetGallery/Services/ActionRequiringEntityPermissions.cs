// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;

namespace NuGetGallery
{
    /// <summary>
    /// An action requiring permissions on an entity that can be done on behalf of another <see cref="User"/>.
    /// </summary>
    public abstract class ActionRequiringEntityPermissions<TEntity>
        : IActionRequiringEntityPermissions<TEntity>
    {
        public PermissionsRequirement AccountOnBehalfOfPermissionsRequirement { get; }

        public ActionRequiringEntityPermissions(PermissionsRequirement accountOnBehalfOfPermissionsRequirement)
        {
            AccountOnBehalfOfPermissionsRequirement = accountOnBehalfOfPermissionsRequirement;
        }

        public PermissionsCheckResult CheckPermissions(User currentUser, User account, TEntity entity)
        {
            if (!PermissionsHelpers.IsRequirementSatisfied(AccountOnBehalfOfPermissionsRequirement, currentUser, account))
            {
                return PermissionsCheckResult.AccountFailure;
            }
            
            return CheckPermissionsForEntity(account, entity);
        }

        public PermissionsCheckResult CheckPermissions(IPrincipal currentPrincipal, User account, TEntity entity)
        {
            if (!PermissionsHelpers.IsRequirementSatisfied(AccountOnBehalfOfPermissionsRequirement, currentPrincipal, account))
            {
                return PermissionsCheckResult.AccountFailure;
            }

            return CheckPermissionsForEntity(account, entity);
        }
        
        protected abstract PermissionsCheckResult CheckPermissionsForEntity(User account, TEntity entity);

        public bool TryGetAccountsAllowedOnBehalfOf(User currentUser, TEntity entity, out IEnumerable<User> accountsAllowedOnBehalfOf)
        {
            accountsAllowedOnBehalfOf = new List<User>();

            var possibleAccountsOnBehalfOf = 
                new[] { currentUser }
                    .Concat(GetOwners(entity));

            if (currentUser != null)
            {
                possibleAccountsOnBehalfOf = 
                    possibleAccountsOnBehalfOf
                        .Concat(currentUser.Organizations.Select(o => o.Organization));
            }

            possibleAccountsOnBehalfOf = possibleAccountsOnBehalfOf.Distinct(new UserEqualityComparer());

            foreach (var accountOnBehalfOf in possibleAccountsOnBehalfOf)
            {
                var result = CheckPermissions(currentUser, accountOnBehalfOf, entity);
                if (result == PermissionsCheckResult.Allowed)
                {
                    (accountsAllowedOnBehalfOf as List<User>).Add(accountOnBehalfOf);
                }
            }

            return accountsAllowedOnBehalfOf.Any();
        }

        protected abstract IEnumerable<User> GetOwners(TEntity entity);

        private class UserEqualityComparer : IEqualityComparer<User>
        {
            public bool Equals(User x, User y)
            {
                return x.MatchesUser(y);
            }

            public int GetHashCode(User obj)
            {
                return obj.Key.GetHashCode();
            }
        }
    }
}