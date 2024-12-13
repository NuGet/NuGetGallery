// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data;
using System.Text.Json;
using System.Threading.Tasks;
using NuGet.Services.Entities;
using NuGetGallery.Authentication;
using NuGetGallery.Infrastructure.Authentication;

#nullable enable

namespace NuGetGallery.Services.Authentication
{
    public interface IFederatedCredentialService
    {
        /// <summary>
        /// Generates a short-lived API key for the user based on the provided bearer token. The user's federated
        /// credential policies are used to evaluate the bearer token and find desired API key settings.
        /// </summary>
        /// <param name="username">The username of the user account that owns the federated credential policy.</param>
        /// <param name="bearerToken">The bearer token to use for federated credential evaluation.</param>
        /// <returns>The result, successful if <see cref="GenerateApiKeyResult.Type"/> is <see cref="GenerateApiKeyResultType.Created"/>.</returns>
        Task<GenerateApiKeyResult> GenerateApiKeyAsync(string username, string bearerToken);

        /// <summary>
        /// Adds a new federated credential policy for an Entra ID service principal. The policy will be owned by the user account
        /// <paramref name="user"/>. Any API keys created from the policy will be scoped to package owner specified by <paramref name="packageOwner"/>.
        /// account
        /// </summary>
        /// <param name="user">The user account to own the policy.</param>
        /// <param name="packageOwner">The owner scope for any API key created from the policy.</param>
        /// <param name="criteria">The Entra ID service principal criteria to allow.</param>
        /// <returns>The result, successful if <see cref="AddFederatedCredentialPolicyResult.Type"/> is <see cref="AddFederatedCredentialPolicyResultType.Created"/>.</returns>
        Task<AddFederatedCredentialPolicyResult> AddEntraIdServicePrincipalPolicyAsync(User user, User packageOwner, EntraIdServicePrincipalCriteria criteria);

        /// <summary>
        /// Deletes a given federated credential policy and all associated API keys.
        /// </summary>
        /// <param name="policy">The policy to delete.</param>
        Task DeletePolicyAsync(FederatedCredentialPolicy policy);
    }

    public class FederatedCredentialService : IFederatedCredentialService
    {
        private readonly IUserService _userService;
        private readonly IFederatedCredentialRepository _repository;
        private readonly IFederatedCredentialPolicyEvaluator _evaluator;
        private readonly IEntraIdTokenValidator _entraIdTokenValidator;
        private readonly ICredentialBuilder _credentialBuilder;
        private readonly IAuthenticationService _authenticationService;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IFeatureFlagService _featureFlagService;
        private readonly IFederatedCredentialConfiguration _configuration;

        public FederatedCredentialService(
            IUserService userService,
            IFederatedCredentialRepository repository,
            IFederatedCredentialPolicyEvaluator evaluator,
            IEntraIdTokenValidator entraIdTokenValidator,
            ICredentialBuilder credentialBuilder,
            IAuthenticationService authenticationService,
            IDateTimeProvider dateTimeProvider,
            IFeatureFlagService featureFlagService,
            IFederatedCredentialConfiguration configuration)
        {
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
            _entraIdTokenValidator = entraIdTokenValidator ?? throw new ArgumentNullException(nameof(EntraIdTokenValidator));
            _credentialBuilder = credentialBuilder ?? throw new ArgumentNullException(nameof(credentialBuilder));
            _authenticationService = authenticationService ?? throw new ArgumentNullException(nameof(authenticationService));
            _dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
            _featureFlagService = featureFlagService ?? throw new ArgumentNullException(nameof(featureFlagService));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public async Task<AddFederatedCredentialPolicyResult> AddEntraIdServicePrincipalPolicyAsync(User createdBy, User packageOwner, EntraIdServicePrincipalCriteria criteria)
        {
            if (createdBy is Organization)
            {
                return AddFederatedCredentialPolicyResult.BadRequest(
                    $"Policy user '{createdBy.Username}' is an organization. Creating federated credential trust policies for organizations is not supported.");
            }

            var testScope = new Scope(packageOwner, NuGetPackagePattern.AllInclusivePattern, NuGetScopes.All);
            if (!_credentialBuilder.VerifyScopes(createdBy, [testScope]))
            {
                return AddFederatedCredentialPolicyResult.BadRequest(
                    $"The user '{createdBy.Username}' does not have the required permissions to add a federated credential policy for package owner '{packageOwner.Username}'.");
            }

            if (!_featureFlagService.CanUseFederatedCredentials(packageOwner))
            {
                return AddFederatedCredentialPolicyResult.BadRequest(NotInFlightMessage(packageOwner));
            }

            if (!_entraIdTokenValidator.IsTenantAllowed(criteria.TenantId))
            {
                return AddFederatedCredentialPolicyResult.BadRequest($"The Entra ID tenant '{criteria.TenantId}' is not in the allow list.");
            }

            var policy = new FederatedCredentialPolicy
            {
                Created = _dateTimeProvider.UtcNow,
                CreatedBy = createdBy,
                PackageOwner = packageOwner,
                Type = FederatedCredentialType.EntraIdServicePrincipal,
                Criteria = JsonSerializer.Serialize(criteria),
            };

            await _repository.AddPolicyAsync(policy, saveChanges: true);

            return AddFederatedCredentialPolicyResult.Created(policy);
        }

        public async Task DeletePolicyAsync(FederatedCredentialPolicy policy)
        {
            var credentials = _repository.GetShortLivedApiKeysForPolicy(policy.Key);
            foreach (var credential in credentials)
            {
                await _authenticationService.RemoveCredential(policy.CreatedBy, credential, commitChanges: false);
            }

            await _repository.DeletePolicyAsync(policy, saveChanges: true);
        }

        public async Task<GenerateApiKeyResult> GenerateApiKeyAsync(string username, string bearerToken)
        {
            var currentUser = _userService.FindByUsername(username, includeDeleted: false);
            if (currentUser is null)
            {
                return NoMatchingPolicy(username);
            }

            var policies = _repository.GetPoliciesCreatedByUser(currentUser.Key);
            var policyEvaluation = await _evaluator.GetMatchingPolicyAsync(policies, bearerToken);
            switch (policyEvaluation.Type)
            {
                case EvaluatedFederatedCredentialPoliciesType.BadToken:
                    return GenerateApiKeyResult.Unauthorized(policyEvaluation.UserError);
                case EvaluatedFederatedCredentialPoliciesType.NoMatchingPolicy:
                    return NoMatchingPolicy(username);
                case EvaluatedFederatedCredentialPoliciesType.MatchedPolicy:
                    break;
                default:
                    throw new NotImplementedException("Unexpected result type: " + policyEvaluation.Type);
            }

            // perform validations after the policy evaluation to avoid leaking information about the related users

            var currentUserError = ValidateCurrentUser(currentUser);
            if (currentUserError != null)
            {
                return currentUserError;
            }

            var packageOwner = _userService.FindByKey(policyEvaluation.MatchedPolicy.PackageOwnerUserKey);
            policyEvaluation.MatchedPolicy.PackageOwner = packageOwner;
            var packageOwnerError = ValidatePackageOwner(packageOwner);
            if (packageOwnerError != null)
            {
                return packageOwnerError;
            }

            var apiKeyCredential = _credentialBuilder.CreateShortLivedApiKey(
                _configuration.ShortLivedApiKeyDuration,
                policyEvaluation.MatchedPolicy,
                out var plaintextApiKey);
            if (!_credentialBuilder.VerifyScopes(currentUser, apiKeyCredential.Scopes))
            {
                return GenerateApiKeyResult.BadRequest(
                    $"The scopes on the generated API key are not valid. " +
                    $"Confirm that you still have permissions to operate on behalf of package owner '{packageOwner.Username}'.");
            }

            var saveError = await SaveAndRejectReplayAsync(currentUser, policyEvaluation, apiKeyCredential);
            if (saveError is not null)
            {
                return saveError;
            }

            return GenerateApiKeyResult.Created(plaintextApiKey, apiKeyCredential.Expires!.Value);
        }

        private static GenerateApiKeyResult NoMatchingPolicy(string username)
        {
            return GenerateApiKeyResult.Unauthorized($"No matching federated credential trust policy owned by user '{username}' was found.");
        }

        private async Task<GenerateApiKeyResult?> SaveAndRejectReplayAsync(
            User currentUser,
            EvaluatedFederatedCredentialPolicies evaluation,
            Credential apiKeyCredential)
        {
            evaluation.MatchedPolicy.LastMatched = _dateTimeProvider.UtcNow;

            await _repository.SaveFederatedCredentialAsync(evaluation.FederatedCredential, saveChanges: false);

            try
            {
                await _authenticationService.AddCredential(currentUser, apiKeyCredential);
            }
            catch (DataException ex) when (ex.IsSqlUniqueConstraintViolation())
            {
                return GenerateApiKeyResult.Unauthorized("This bearer token has already been used. A new bearer token must be used for each request.");
            }

            return null;
        }

        private static GenerateApiKeyResult? ValidateCurrentUser(User currentUser)
        {
            if (currentUser is Organization)
            {
                return GenerateApiKeyResult.BadRequest(
                    "Generating fetching tokens directly for organizations is not supported. " +
                    "The federated credential trust policy is created on the profile of one of the organization's administrators and is scoped to the organization in the policy.");
            }

            var error = GetUserStateError(currentUser);
            if (error != null)
            {
                return error;
            }

            return null;
        }

        private GenerateApiKeyResult? ValidatePackageOwner(User? packageOwner)
        {
            if (packageOwner is null)
            {
                return GenerateApiKeyResult.BadRequest("The package owner of the match federated credential trust policy not longer exists.");
            }

            var error = GetUserStateError(packageOwner);
            if (error != null)
            {
                return error;
            }

            if (!_featureFlagService.CanUseFederatedCredentials(packageOwner))
            {
                return GenerateApiKeyResult.BadRequest(NotInFlightMessage(packageOwner));
            }

            return null;
        }

        private static string NotInFlightMessage(User packageOwner)
        {
            return $"The package owner '{packageOwner.Username}' is not enabled to use federated credentials.";
        }

        private static GenerateApiKeyResult? GetUserStateError(User user)
        {
            var orgOrUser = user is Organization ? "organization" : "user";

            if (user.IsDeleted)
            {
                return GenerateApiKeyResult.BadRequest($"The {orgOrUser} '{user.Username}' is deleted.");
            }

            if (user.IsLocked)
            {
                return GenerateApiKeyResult.BadRequest($"The {orgOrUser} '{user.Username}' is locked.");
            }

            if (!user.Confirmed)
            {
                return GenerateApiKeyResult.BadRequest($"The {orgOrUser} '{user.Username}' does not have a confirmed email address.");
            }

            return null;
        }
    }
}
