// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.Entities;
using System.Threading.Tasks;
using NuGetGallery.Authentication;
using NuGetGallery.Infrastructure.Authentication;
using System.Linq;
using System.Data;

#nullable enable

namespace NuGetGallery.Services.Authentication
{
    public interface IFederatedCredentialService
    {
        Task<GenerateApiKeyResult> GenerateApiKeyAsync(string username, string bearerToken);
    }

    public class FederatedCredentialService : IFederatedCredentialService
    {
        private readonly IUserService _userService;
        private readonly IEntityRepository<FederatedCredentialPolicy> _policyRepository;
        private readonly IEntityRepository<FederatedCredential> _federatedCredentialRepository;
        private readonly IFederatedCredentialEvaluator _evaluator;
        private readonly ICredentialBuilder _credentialBuilder;
        private readonly IAuthenticationService _authenticationService;
        private readonly IFeatureFlagService _featureFlagService;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IFederatedCredentialConfiguration _configuration;

        public FederatedCredentialService(
            IUserService userService,
            IEntityRepository<FederatedCredentialPolicy> policyRepository,
            IEntityRepository<FederatedCredential> federatedCredentialRepository,
            IFederatedCredentialEvaluator evaluator,
            ICredentialBuilder credentialBuilder,
            IAuthenticationService authenticationService,
            IFeatureFlagService featureFlagService,
            IDateTimeProvider dateTimeProvider,
            IFederatedCredentialConfiguration configuration)
        {
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
            _policyRepository = policyRepository ?? throw new ArgumentNullException(nameof(policyRepository));
            _federatedCredentialRepository = federatedCredentialRepository ?? throw new ArgumentNullException(nameof(federatedCredentialRepository));
            _evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
            _credentialBuilder = credentialBuilder ?? throw new ArgumentNullException(nameof(credentialBuilder));
            _authenticationService = authenticationService ?? throw new ArgumentNullException(nameof(authenticationService));
            _featureFlagService = featureFlagService ?? throw new ArgumentNullException(nameof(featureFlagService));
            _dateTimeProvider = dateTimeProvider;
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public async Task<GenerateApiKeyResult> GenerateApiKeyAsync(string username, string bearerToken)
        {
            var currentUser = _userService.FindByUsername(username, includeDeleted: false);
            if (currentUser is null)
            {
                return GenerateApiKeyResult.BadRequest($"No user with username '{username}' exists.");
            }

            if (currentUser is Organization)
            {
                return GenerateApiKeyResult.BadRequest(
                    "Generating fetching tokens directly for organizations is not supported. " +
                    "The federated credential trust policy is created on the profile of one of the organization's administrators and is scoped to the organization in the policy.");
            }

            var evaluation = await GetMatchingPolicyAsync(currentUser, bearerToken);
            if (!evaluation.HasMatchingPolicy)
            {
                return GenerateApiKeyResult.Unauthorized("No matching federated credential trust policy found.");
            }

            var currentUserError = GetUserStateErrorMessage(currentUser);
            if (currentUserError != null)
            {
                return GenerateApiKeyResult.BadRequest(currentUserError);
            }

            var scopeOwner = _userService.FindByKey(evaluation.SelectedPolicy.OwnerKey);

            if (!_featureFlagService.CanUseFederatedCredentials(scopeOwner))
            {
                return GenerateApiKeyResult.BadRequest(
                    $"The package owner '{scopeOwner.Username}' is not enabled to use federated credentials.");
            }

            var scopeOwnerError = GetUserStateErrorMessage(scopeOwner);
            if (scopeOwnerError != null)
            {
                return GenerateApiKeyResult.BadRequest(scopeOwnerError);
            }

            var (credential, plaintextApiKey) = CreateShortLivedApiKey(scopeOwner, evaluation.SelectedPolicy);
            if (!_credentialBuilder.VerifyScopes(currentUser, credential.Scopes))
            {
                return GenerateApiKeyResult.BadRequest(
                    $"The scopes on the generated API key are not valid. " +
                    $"Confirm that you still have permissions to operate on behalf of package owner '{scopeOwner.Username}'.");
            }

            var saved = await SaveAndRejectReplayAsync(currentUser, evaluation, credential);
            if (!saved)
            {
                return GenerateApiKeyResult.Unauthorized("This bearer token has already been used. A new bearer token must be used for each request.");
            }

            return GenerateApiKeyResult.Created(plaintextApiKey, credential.Expires!.Value);
        }

        private static string? GetUserStateErrorMessage(User user)
        {
            if (user.IsDeleted)
            {
                return $"The user '{user.Username}' is deleted.";
            }

            if (user.IsLocked)
            {
                return $"The user '{user.Username}' is locked.";
            }

            if (!user.Confirmed)
            {
                return $"The user '{user.Username}' does not have a confirmed email address.";
            }

            return null;
        }

        private async Task<EvaluatedFederatedCredentialPolicies> GetMatchingPolicyAsync(User currentUser, string bearerToken)
        {
            var policies = _policyRepository
                .GetAll()
                .Where(p => p.UserKey == currentUser.Key)
                .ToList();
            var evaluation = await _evaluator.GetMatchingPolicyAsync(policies, bearerToken);
            return evaluation;
        }

        private bool HasFederatedCredentialBeenUsed(string federatedCredentialIdentifier)
        {
            return _federatedCredentialRepository
                .GetAll()
                .Where(c => c.Identity == federatedCredentialIdentifier)
                .Any();
        }

        private (Credential Credential, string PlainTextApiKey) CreateShortLivedApiKey(User scopeOwner, FederatedCredentialPolicy policy)
        {
            var expiration = _configuration.ShortLivedApiKeyDuration;
            var credential = _credentialBuilder.CreateApiKey(expiration, out var plaintextApiKey);

            credential.FederatedCredentialPolicyKey = policy.Key;
            credential.Description = "Short-lived API key generated via a federated credential";
            credential.Scopes = _credentialBuilder.BuildScopes(
                scopeOwner,
                scopes: [NuGetScopes.All],
                subjects: [NuGetPackagePattern.AllInclusivePattern]);

            return (credential, plaintextApiKey);
        }

        private async Task<bool> SaveAndRejectReplayAsync(User currentUser, EvaluatedFederatedCredentialPolicies evaluation, Credential credential)
        {
            evaluation.SelectedPolicy.LastMatched = _dateTimeProvider.UtcNow;

            _federatedCredentialRepository.InsertOnCommit(evaluation.FederatedCredential);

            try
            {
                await _authenticationService.AddCredential(currentUser, credential);
                return true;
            }
            catch (DataException ex) when (ex.IsSqlUniqueConstraintViolation())
            {
                return false;
            }
        }
    }
}
