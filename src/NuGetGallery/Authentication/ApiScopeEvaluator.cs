using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Web;

namespace NuGetGallery.Authentication
{
    public class ApiScopeEvaluator : IApiScopeEvaluator
    {
        private TypeConverter Converter = new ScopeSubjectTypeConverter();

        private IUserService UserService { get; }

        public ApiScopeEvaluator(IUserService userService)
        {
            UserService = userService;
        }

        // Unit test constructor
        public ApiScopeEvaluator(IUserService userService, TypeConverter converter)
            : this(userService)
        {
            Converter = converter;
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
        public ApiScopeEvaluationResult Evaluate<TEntity>(
            User currentUser,
            IEnumerable<Scope> scopes,
            IActionRequiringEntityPermissions<TEntity> action,
            TEntity entity,
            out User owner,
            params string[] requestedActions)
        {
            owner = null;

            if (scopes == null || !scopes.Any())
            {
                // Legacy V1 API key without scopes.
                // Evaluate it as if it has an unlimited scope.
                scopes = new[] { new Scope(ownerKey: null, subject: NuGetPackagePattern.AllInclusivePattern, allowedAction: NuGetScopes.All) };
            }

            var failureResult = ApiScopeEvaluationResult.Unknown;

            foreach (var scope in scopes)
            {
                if (!scope.AllowsSubject(ConvertToScopeSubject(entity)))
                {
                    // Subject (package ID) does not match.
                    failureResult = ChooseFailureResult(failureResult, ApiScopeEvaluationResult.Forbidden);
                    continue;
                }

                if (!scope.AllowsActions(requestedActions))
                {
                    // Action scopes does not match.
                    failureResult = ChooseFailureResult(failureResult, ApiScopeEvaluationResult.Forbidden);
                    continue;
                }

                // Get the owner from the scope.
                // If the scope has no owner, use the current user.
                var ownerInScope = scope.HasOwnerScope() ? UserService.FindByKey(scope.OwnerKey.Value) : currentUser;

                var isActionAllowed = action.CheckPermissions(currentUser, ownerInScope, entity);
                if (isActionAllowed != PermissionsCheckResult.Allowed)
                {
                    // Current user cannot do the action on behalf of the owner in the scope or owner in the scope is not allowed to do the action.
                    var currentFailureResult = ApiScopeEvaluationResult.Forbidden;
                    if (isActionAllowed == PermissionsCheckResult.ReservedNamespaceFailure)
                    {
                        currentFailureResult = ApiScopeEvaluationResult.ConflictReservedNamespace;
                    }

                    failureResult = ChooseFailureResult(failureResult, currentFailureResult);
                    continue;
                }

                owner = ownerInScope;
                return ApiScopeEvaluationResult.Success;
            }

            return failureResult;
        }

        /// <summary>
        /// Determines the <see cref="ApiScopeEvaluationResult"/> to return from <see cref="EvaluateApiScope(IScopeSubject, out User, string[])"/> when no <see cref="Scope"/>s return <see cref="ApiScopeEvaluationResult.Success"/>.
        /// </summary>
        /// <param name="last">The result of the <see cref="Scope"/>s that have been evaluated so far.</param>
        /// <param name="next">The result of the <see cref="Scope"/> that was just evaluated.</param>
        private ApiScopeEvaluationResult ChooseFailureResult(ApiScopeEvaluationResult last, ApiScopeEvaluationResult next)
        {
            return new[] { last, next }.Max();
        }

        private string ConvertToScopeSubject(object value)
        {
            var type = value?.GetType() ?? null;

            if (!Converter.CanConvertFrom(type))
            {
                throw new InvalidCastException($"Cannot convert {type} to a scope subject!");
            }

            return Converter.ConvertFrom(value) as string;
        }
    }
}