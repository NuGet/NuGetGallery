using System;
using System.Collections.Generic;

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
        /// <param name="owner">
        /// The <see cref="User"/> specified by the <see cref="Scope.OwnerKey"/> of the <see cref="Scope"/> that evaluated with <see cref="ApiScopeEvaluationResult.Success"/>.
        /// If no <see cref="Scope"/>s evaluate to <see cref="ApiScopeEvaluationResult.Success"/>, this will be null.
        /// </param>
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
        /// <param name="package">The <see cref="Package"/> that the scopes being evaluated are checked for permission to <paramref name="action"/> on.</param>
        /// <param name="owner">
        /// The <see cref="User"/> specified by the <see cref="Scope.OwnerKey"/> of the <see cref="Scope"/> that evaluated with <see cref="ApiScopeEvaluationResult.Success"/>.
        /// If no <see cref="Scope"/>s evaluate to <see cref="ApiScopeEvaluationResult.Success"/>, this will be null.
        /// </param>
        /// <returns>A <see cref="ApiScopeEvaluationResult"/> that describes the evaluation of the <see cref="Scope"/>s.</returns>
        ApiScopeEvaluationResult Evaluate(
            User currentUser,
            IEnumerable<Scope> scopes,
            IActionRequiringEntityPermissions<Package> action,
            Package package,
            params string[] requestedActions);

        /// <summary>
        /// Evaluates the whether or not an action is allowed given a set of <paramref name="scopes"/>, an <paramref name="action"/>, and the <paramref name="requestedActions"/>.
        /// </summary>
        /// <param name="currentUser">The current user attempting to do the action with the given <paramref name="scopes"/>.</param>
        /// <param name="scopes">The scopes being evaluated.</param>
        /// <param name="action">The action that the scopes being evaluated are checked for permission to do.</param>
        /// <param name="context">The <see cref="ActionOnNewPackageContext"/> that the scopes being evaluated are checked for permission to <paramref name="action"/> on.</param>
        /// <param name="owner">
        /// The <see cref="User"/> specified by the <see cref="Scope.OwnerKey"/> of the <see cref="Scope"/> that evaluated with <see cref="ApiScopeEvaluationResult.Success"/>.
        /// If no <see cref="Scope"/>s evaluate to <see cref="ApiScopeEvaluationResult.Success"/>, this will be null.
        /// </param>
        /// <returns>A <see cref="ApiScopeEvaluationResult"/> that describes the evaluation of the <see cref="Scope"/>s.</returns>
        ApiScopeEvaluationResult Evaluate(
            User currentUser,
            IEnumerable<Scope> scopes,
            IActionRequiringEntityPermissions<ActionOnNewPackageContext> action,
            ActionOnNewPackageContext context,
            params string[] requestedActions);
    }
}
