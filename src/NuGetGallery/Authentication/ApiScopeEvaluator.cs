// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGetGallery.Authentication
{
    public class ApiScopeEvaluator : IApiScopeEvaluator
    {
        private IUserService UserService { get; }

        public ApiScopeEvaluator(IUserService userService)
        {
            UserService = userService;
        }

        /// <summary>
        /// Evaluates the whether or not an action is allowed given a set of <paramref name="scopes"/>, an <paramref name="action"/>, and the <paramref name="requestedActions"/>.
        /// </summary>
        /// <param name="currentUser">The current user attempting to do the action with the given <paramref name="scopes"/>.</param>
        /// <param name="scopes">The scopes being evaluated.</param>
        /// <param name="action">The action that the scopes being evaluated are checked for permission to do.</param>
        /// <param name="entity">The entity that the scopes being evaluated are checked for permission to <paramref name="action"/> on.</param>
        /// <param name="owner">
        /// The <see cref="User"/> specified by the <see cref="Scope.OwnerKey"/> of the <see cref="Scope"/> that evaluated with <see cref="ApiScopeEvaluationResult.Success"/>.
        /// If no <see cref="Scope"/>s evaluate to <see cref="ApiScopeEvaluationResult.Success"/>, this will be null.
        /// </param>
        /// <returns>A <see cref="ApiScopeEvaluationResult"/> that describes the evaluation of the .</returns>
        /// <remarks>This method is internal because it is tested directly.</remarks>
        internal ApiScopeEvaluationResult Evaluate<TEntity>(
            User currentUser,
            IEnumerable<Scope> scopes,
            IActionRequiringEntityPermissions<TEntity> action,
            TEntity entity,
            Func<TEntity, string> getSubjectFromEntity,
            params string[] requestedActions)
        {
            User ownerInScope = null;

            if (scopes == null || !scopes.Any())
            {
                // Legacy V1 API key without scopes.
                // Evaluate it as if it has an unlimited scope.
                scopes = new[] { new Scope(ownerKey: null, subject: NuGetPackagePattern.AllInclusivePattern, allowedAction: NuGetScopes.All) };
            }

            var aggregateResult = new ApiScopeEvaluationResult(true, PermissionsCheckResult.Unknown, null);

            foreach (var scope in scopes)
            {
                if (!scope.AllowsSubject(getSubjectFromEntity(entity)))
                {
                    // Subject (package ID) does not match.
                    aggregateResult = ChooseFailureResult(aggregateResult, new ApiScopeEvaluationResult(false, PermissionsCheckResult.Unknown, null));
                    continue;
                }

                if (!scope.AllowsActions(requestedActions))
                {
                    // Action scopes does not match.
                    aggregateResult = ChooseFailureResult(aggregateResult, new ApiScopeEvaluationResult(false, PermissionsCheckResult.Unknown, null));
                    continue;
                }

                // Get the owner from the scope.
                // If the scope has no owner, use the current user.
                int ownerInScopeKey = scope.HasOwnerScope() ? scope.OwnerKey.Value : currentUser.Key;
                if (ownerInScope == null)
                {
                    ownerInScope = UserService.FindByKey(ownerInScopeKey);
                }
                
                if (ownerInScopeKey != ownerInScope.Key)
                {
                    // The set of scopes contains multiple owners. This should not be possible.
                    aggregateResult = ChooseFailureResult(aggregateResult, new ApiScopeEvaluationResult(false, PermissionsCheckResult.Unknown, null));
                    continue;
                }

                var isActionAllowed = action.CheckPermissions(currentUser, ownerInScope, entity);
                var result = new ApiScopeEvaluationResult(true, isActionAllowed, ownerInScope);
                aggregateResult = ChooseFailureResult(aggregateResult, result);
                if (isActionAllowed != PermissionsCheckResult.Allowed)
                {
                    // Current user cannot do the action on behalf of the owner in the scope or owner in the scope is not allowed to do the action.
                    continue;
                }

                return result;
            }

            return aggregateResult;
        }

        private ApiScopeEvaluationResult ChooseFailureResult(params ApiScopeEvaluationResult[] results)
        {
            return results.Max();
        }

        public ApiScopeEvaluationResult Evaluate(
            User currentUser, 
            IEnumerable<Scope> scopes, 
            IActionRequiringEntityPermissions<PackageRegistration> action, 
            PackageRegistration packageRegistration, 
            params string[] requestedActions)
        {
            return Evaluate(currentUser, scopes, action, packageRegistration, (pr) => pr.Id, requestedActions);
        }

        public ApiScopeEvaluationResult Evaluate(
            User currentUser, 
            IEnumerable<Scope> scopes, 
            IActionRequiringEntityPermissions<Package> action, 
            Package package, 
            params string[] requestedActions)
        {
            return Evaluate(currentUser, scopes, action, package, (p) => p.PackageRegistration.Id, requestedActions);
        }

        public ApiScopeEvaluationResult Evaluate(
            User currentUser, 
            IEnumerable<Scope> scopes, 
            IActionRequiringEntityPermissions<ActionOnNewPackageContext> action, 
            ActionOnNewPackageContext context, 
            params string[] requestedActions)
        {
            return Evaluate(currentUser, scopes, action, context, (c) => c.PackageId, requestedActions);
        }
    }
}