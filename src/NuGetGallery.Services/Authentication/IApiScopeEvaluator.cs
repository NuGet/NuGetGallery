// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Services.Entities;

namespace NuGetGallery.Authentication
{
    public interface IApiScopeEvaluator
    {
        /// <summary>
        /// Evaluates the whether or not an action is allowed given a set of <paramref name="scopes"/>, an <paramref name="action"/>, and the <paramref name="requestedActions"/>.
        /// </summary>
        /// <param name="currentUser">The current user attempting to do the action with the given <paramref name="scopes"/>.</param>
        /// <param name="scopes">The scopes being evaluated.</param>
        /// <param name="action">The action that the scopes being evaluated are checked for permission to do.</param>
        /// <param name="packageRegistration">The <see cref="PackageRegistration"/> that the scopes being evaluated are checked for permission to <paramref name="action"/> on.</param>
        /// <param name="requestedActions">A list of actions that the scopes must match.</param>
        /// <returns>A <see cref="ApiScopeEvaluationResult"/> that describes the evaluation of the <see cref="Scope"/>s.</returns>
        ApiScopeEvaluationResult Evaluate(
            User currentUser,
            IEnumerable<Scope> scopes,
            IActionRequiringEntityPermissions<PackageRegistration> action,
            PackageRegistration packageRegistration,
            params string[] requestedActions);

        /// <summary>
        /// Evaluates the whether or not an action is allowed given a set of <paramref name="scopes"/>, an <paramref name="action"/>, and the <paramref name="requestedActions"/>.
        /// </summary>
        /// <param name="currentUser">The current user attempting to do the action with the given <paramref name="scopes"/>.</param>
        /// <param name="scopes">The scopes being evaluated.</param>
        /// <param name="action">The action that the scopes being evaluated are checked for permission to do.</param>
        /// <param name="context">The <see cref="ActionOnNewPackageContext"/> that the scopes being evaluated are checked for permission to <paramref name="action"/> on.</param>
        /// <param name="requestedActions">A list of actions that the scopes must match.</param>
        /// <returns>A <see cref="ApiScopeEvaluationResult"/> that describes the evaluation of the <see cref="Scope"/>s.</returns>
        ApiScopeEvaluationResult Evaluate(
            User currentUser,
            IEnumerable<Scope> scopes,
            IActionRequiringEntityPermissions<ActionOnNewPackageContext> action,
            ActionOnNewPackageContext context,
            params string[] requestedActions);
    }
}
