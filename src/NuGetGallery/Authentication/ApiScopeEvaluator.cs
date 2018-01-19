// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGetGallery.Authentication
{
    public class ApiScopeEvaluator : IApiScopeEvaluator
    {
        private readonly IUserService _userService;

        public ApiScopeEvaluator(IUserService userService)
        {
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
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

            // Check that all scopes provided have the same owner scope.
            var ownerScopes = scopes.Select(s => s.OwnerKey);
            var ownerScope = ownerScopes.FirstOrDefault();
            if (ownerScopes.Any(o => o != ownerScope))
            {
                throw new ArgumentException("All scopes provided must have the same owner scope.");
            }

            var matchingScope = scopes
                .FirstOrDefault(scope => 
                    scope.AllowsSubject(getSubjectFromEntity(entity)) && 
                    scope.AllowsActions(requestedActions));

            ownerInScope = ownerScope.HasValue ? _userService.FindByKey(ownerScope.Value) : currentUser;

            if (matchingScope == null)
            {
                return new ApiScopeEvaluationResult(ownerInScope, PermissionsCheckResult.Unknown, scopesAreValid: false);
            }
            
            var isActionAllowed = action.CheckPermissions(currentUser, ownerInScope, entity);
            return new ApiScopeEvaluationResult(ownerInScope, isActionAllowed, scopesAreValid: true);
        }
    }
}