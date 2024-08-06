// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Security.Principal;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public interface IActionRequiringEntityPermissions
    {
        /// <summary>
        /// Determines whether <paramref name="currentUser"/> can perform this action on an arbitrary <see cref="TEntity"/> on behalf of <paramref name="account"/>.
        /// </summary>
        /// <returns>True if and only if <paramref name="currentUser"/> can perform this action on an arbitrary <see cref="TEntity"/> on behalf of <paramref name="account"/>.</returns>
        bool IsAllowedOnBehalfOfAccount(User currentUser, User account);

        /// <summary>
        /// Determines whether <paramref name="currentPrincipal"/> can perform this action on an arbitrary <see cref="TEntity"/> on behalf of <paramref name="account"/>.
        /// </summary>
        /// <returns>True if and only if <paramref name="currentPrincipal"/> can perform this action on an arbitrary <see cref="TEntity"/> on behalf of <paramref name="account"/>.</returns>
        bool IsAllowedOnBehalfOfAccount(IPrincipal currentPrincipal, User account);
    }

    public interface IActionRequiringEntityPermissions<TEntity> : IActionRequiringEntityPermissions
    {
        /// <summary>
        /// Determines whether <paramref name="currentUser"/> can perform this action on <paramref name="entity"/> on behalf of <paramref name="account"/>.
        /// </summary>
        /// <returns>
        /// A <see cref="PermissionsCheckResult"/> describing whether or not the action can be performed.
        /// </returns>
        PermissionsCheckResult CheckPermissions(User currentUser, User account, TEntity entity);

        /// <summary>
        /// Determines whether <paramref name="currentPrincipal"/> can perform this action on <paramref name="entity"/> on behalf of <paramref name="account"/>.
        /// </summary>
        /// <returns>
        /// A <see cref="PermissionsCheckResult"/> describing whether or not the action can be performed.
        /// </returns>
        PermissionsCheckResult CheckPermissions(IPrincipal currentPrincipal, User account, TEntity entity);

        /// <summary>
        /// Determines whether <paramref name="currentUser"/> can perform this action on <paramref name="entity"/> on behalf of any <see cref="User"/>.
        /// </summary>
        /// <returns>True if and only if <paramref name="currentUser"/> can perform this action on <paramref name="entity"/> on behalf of any <see cref="User"/>.</returns>
        PermissionsCheckResult CheckPermissionsOnBehalfOfAnyAccount(User currentUser, TEntity entity);

        /// <summary>
        /// Determines whether <paramref name="currentPrincipal"/> can perform this action on <paramref name="entity"/> on behalf of any <see cref="User"/>.
        /// </summary>
        /// <param name="accountsAllowedOnBehalfOf">A <see cref="IEnumerable{User}"/> containing all accounts that <paramref name="currentUser"/> can perform this action on <paramref name="entity"/> on behalf of.</param>
        /// <returns>True if and only if <paramref name="currentPrincipal"/> can perform this action on <paramref name="entity"/> on behalf of any <see cref="User"/>.</returns>
        PermissionsCheckResult CheckPermissionsOnBehalfOfAnyAccount(User currentUser, TEntity entity, out IEnumerable<User> accountsAllowedOnBehalfOf);
    }
}