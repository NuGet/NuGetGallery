// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data;
using System.Text.Json;
using System.Threading.Tasks;
using NuGet.Services.Entities;
using NuGetGallery.Auditing;
using NuGetGallery.Authentication;
using NuGetGallery.Configuration;
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
        /// <param name="requestHeaders">The HTTP headers used for the request. This provides full context needed for additional request validation.</param>
        /// <returns>The result, successful if <see cref="GenerateApiKeyResult.Type"/> is <see cref="GenerateApiKeyResultType.Created"/>.</returns>
        Task<GenerateApiKeyResult> GenerateApiKeyAsync(string username, string bearerToken, NameValueCollection requestHeaders);

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
        private readonly IAuditingService _auditingService;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IFeatureFlagService _featureFlagService;
        private readonly IFederatedCredentialConfiguration _configuration;
        private readonly IGalleryConfigurationService _galleryConfigurationService;

        private static readonly IReadOnlyDictionary<string, char> GalleryToApiKeyV5EnvironmentMappings = new Dictionary<string, char>(StringComparer.OrdinalIgnoreCase)
        {
            { ServicesConstants.DevelopmentEnvironment, ApiKeyV5.KnownEnvironments.Local },
            { ServicesConstants.DevEnvironment, ApiKeyV5.KnownEnvironments.Development },
            { ServicesConstants.IntEnvironment, ApiKeyV5.KnownEnvironments.Integration },
            { ServicesConstants.ProdEnvironment, ApiKeyV5.KnownEnvironments.Production },
        };

        public FederatedCredentialService(
            IUserService userService,
            IFederatedCredentialRepository repository,
            IFederatedCredentialPolicyEvaluator evaluator,
            IEntraIdTokenValidator entraIdTokenValidator,
            ICredentialBuilder credentialBuilder,
            IAuthenticationService authenticationService,
            IAuditingService auditingService,
            IDateTimeProvider dateTimeProvider,
            IFeatureFlagService featureFlagService,
            IFederatedCredentialConfiguration configuration,
            IGalleryConfigurationService galleryConfigurationService)
        {
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
            _entraIdTokenValidator = entraIdTokenValidator ?? throw new ArgumentNullException(nameof(EntraIdTokenPolicyValidator));
            _credentialBuilder = credentialBuilder ?? throw new ArgumentNullException(nameof(credentialBuilder));
            _authenticationService = authenticationService ?? throw new ArgumentNullException(nameof(authenticationService));
            _auditingService = auditingService ?? throw new ArgumentNullException(nameof(auditingService));
            _dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
            _featureFlagService = featureFlagService ?? throw new ArgumentNullException(nameof(featureFlagService));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _galleryConfigurationService = galleryConfigurationService ?? throw new ArgumentNullException(nameof(configuration));
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

            await _auditingService.SaveAuditRecordAsync(FederatedCredentialPolicyAuditRecord.Create(policy));

            return AddFederatedCredentialPolicyResult.Created(policy);
        }

        public async Task DeletePolicyAsync(FederatedCredentialPolicy policy)
        {
            var credentials = _repository.GetShortLivedApiKeysForPolicy(policy.Key);
            foreach (var credential in credentials)
            {
                await _authenticationService.RemoveCredential(policy.CreatedBy, credential, commitChanges: false);
            }

            // Initialize the audit record before deletion so details are still available.
            // Entity Framework unlinks navigation properties.
            var auditRecord = FederatedCredentialPolicyAuditRecord.Delete(policy, credentials);

            await _repository.DeletePolicyAsync(policy, saveChanges: true);

            await _auditingService.SaveAuditRecordAsync(auditRecord);
        }

        public async Task<GenerateApiKeyResult> GenerateApiKeyAsync(string username, string bearerToken, NameValueCollection requestHeaders)
        {
            var currentUser = _userService.FindByUsername(username, includeDeleted: false);
            if (currentUser is null)
            {
                return NoMatchingPolicy(username);
            }

            var policies = _repository.GetPoliciesCreatedByUser(currentUser.Key);
            var policyEvaluation = await _evaluator.GetMatchingPolicyAsync(policies, bearerToken, requestHeaders);
            switch (policyEvaluation.Type)
            {
                case OidcTokenEvaluationResultType.BadToken:
                    return GenerateApiKeyResult.Unauthorized(policyEvaluation.UserError);
                case OidcTokenEvaluationResultType.NoMatchingPolicy:
                    return NoMatchingPolicy(username);
                case OidcTokenEvaluationResultType.MatchedPolicy:
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

            var apiKeyV5Environment = ApiKeyV5.KnownEnvironments.Local;
            if (GalleryToApiKeyV5EnvironmentMappings.TryGetValue(_galleryConfigurationService.Current.Environment, out var value))
            {
                apiKeyV5Environment = value;
            }

            var apiKeyCredential = _credentialBuilder.CreateShortLivedApiKey(
                _configuration.ShortLivedApiKeyDuration,
                policyEvaluation.MatchedPolicy,
                apiKeyV5Environment,
                _featureFlagService.IsApiKeyV5EnabledForOIDC(packageOwner),
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
            return GenerateApiKeyResult.Unauthorized($"No matching trust policy owned by user '{username}' was found.");
        }

        private async Task<GenerateApiKeyResult?> SaveAndRejectReplayAsync(
            User currentUser,
            OidcTokenEvaluationResult evaluation,
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
                await _auditingService.SaveAuditRecordAsync(FederatedCredentialPolicyAuditRecord.RejectReplay(
                    evaluation.MatchedPolicy,
                    evaluation.FederatedCredential));

                return GenerateApiKeyResult.Unauthorized("This bearer token has already been used. A new bearer token must be used for each request.");
            }

            await _auditingService.SaveAuditRecordAsync(FederatedCredentialPolicyAuditRecord.ExchangeForApiKey(
                evaluation.MatchedPolicy,
                evaluation.FederatedCredential,
                apiKeyCredential));

            return null;
        }

        private static GenerateApiKeyResult? ValidateCurrentUser(User currentUser)
        {
            if (currentUser is Organization)
            {
                return GenerateApiKeyResult.BadRequest(
                    "Generating fetching tokens directly for organizations is not supported. " +
                    "The trust policy is created on the profile of one of the organization's administrators and is scoped to the organization in the policy.");
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
                return GenerateApiKeyResult.BadRequest("The package owner of the match trust policy not longer exists.");
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
