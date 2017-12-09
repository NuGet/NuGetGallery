// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

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

        /// <summary>
        /// Determines whether <paramref name="currentUser"/> can perform this action on <paramref name="entity"/> on behalf of <paramref name="account"/>.
        /// </summary>
        public PermissionsCheckResult IsAllowed(User currentUser, User account, TEntity entity)
        {
            if (!PermissionsHelpers.IsRequirementSatisfied(AccountOnBehalfOfPermissionsRequirement, currentUser, account))
            {
                return PermissionsCheckResult.AccountFailure;
            }
            
            return IsAllowedOnEntity(account, entity);
        }

        /// <summary>
        /// Determines whether <paramref name="currentPrincipal"/> can perform this action on <paramref name="entity"/> on behalf of <paramref name="account"/>.
        /// </summary>
        public PermissionsCheckResult IsAllowed(IPrincipal currentPrincipal, User account, TEntity entity)
        {
            if (!PermissionsHelpers.IsRequirementSatisfied(AccountOnBehalfOfPermissionsRequirement, currentPrincipal, account))
            {
                return PermissionsCheckResult.AccountFailure;
            }

            return IsAllowedOnEntity(account, entity);
        }
        
        protected abstract PermissionsCheckResult IsAllowedOnEntity(User account, TEntity entity);

        /// <summary>
        /// Determines whether <paramref name="currentPrincipal"/> can perform this action on <paramref name="entity"/> on behalf of any <see cref="User"/>.
        /// </summary>
        /// <param name="accountsAllowedOnBehalfOf">A <see cref="IEnumerable{User}"/> containing all accounts that <paramref name="currentUser"/> can perform this action on <paramref name="entity"/> on behalf of.</param>
        /// <returns>True if and only if <paramref name="currentPrincipal"/> can perform this action on <paramref name="entity"/> on behalf of any <see cref="User"/>.</returns>
        public bool TryGetAccountsIsAllowedOnBehalfOf(User currentUser, TEntity entity, out IEnumerable<User> accountsAllowedOnBehalfOf)
        {
            accountsAllowedOnBehalfOf = Enumerable.Empty<User>();

            var possibleAccountsOnBehalfOf = 
                new[] { currentUser }
                    .Union(GetOwners(entity));

            if (currentUser != null)
            {
                possibleAccountsOnBehalfOf = 
                    possibleAccountsOnBehalfOf
                        .Union(currentUser.Organizations.Select(o => o.Organization));
            }

            foreach (var accountOnBehalfOf in possibleAccountsOnBehalfOf)
            {
                var failure = IsAllowed(currentUser, accountOnBehalfOf, entity);
                if (failure == PermissionsCheckResult.Allowed)
                {
                    accountsAllowedOnBehalfOf = accountsAllowedOnBehalfOf.Concat(new[] { accountOnBehalfOf });
                }
            }

            return accountsAllowedOnBehalfOf.Any();
        }

        protected abstract IEnumerable<User> GetOwners(TEntity entity);
    }
}