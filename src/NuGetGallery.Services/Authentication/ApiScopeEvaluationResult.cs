// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Entities;

namespace NuGetGallery.Authentication
{

    /// <summary>
    /// The result of evaluating the current user's scopes by using <see cref="ApiScopeEvaluator"/>.
    /// </summary>
    public class ApiScopeEvaluationResult
    {
        /// <summary>
        /// True IFF any scope's subject (<see cref="Scope.Subject"/>) and allowed action (<see cref="Scope.AllowedAction"/>) match the subject being acted upon and the action being performed.
        /// </summary>
        public bool ScopesAreValid { get; }

        /// <summary>
        /// If <see cref="ScopesAreValid"/> is true, the <see cref="PermissionsCheckResult"/> returned by checking the permission of the scope's owner (<see cref="Scope.Owner"/>).
        /// Otherwise, <see cref="PermissionsCheckResult.Unknown"/>.
        /// </summary>
        public PermissionsCheckResult PermissionsCheckResult { get; }

        /// <summary>
        /// The owner of the scope as acquired from <see cref="Scope.Owner"/>.
        /// </summary>
        public User Owner { get; }

        public bool IsOwnerConfirmed => Owner != null && Owner.Confirmed;

        public ApiScopeEvaluationResult(User owner, PermissionsCheckResult permissionsCheckResult, bool scopesAreValid)
        {
            ScopesAreValid = scopesAreValid;
            PermissionsCheckResult = permissionsCheckResult;
            Owner = owner;
        }

        /// <summary>
        /// Returns whether or not this <see cref="ApiScopeEvaluationResult"/> represents a successful authentication.
        /// If this <see cref="ApiScopeEvaluationResult"/> does not represent a successful authentication, the current user should be denied from performing the action they are attempting to perform.
        /// </summary>
        public bool IsSuccessful()
        {
            return ScopesAreValid && PermissionsCheckResult == PermissionsCheckResult.Allowed && IsOwnerConfirmed;
        }
    }
}