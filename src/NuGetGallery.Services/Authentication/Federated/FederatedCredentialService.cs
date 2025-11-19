// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data;
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
        /// Adds <see cref="FederatedCredentialPolicy">. Logs an audit record for the policy creation.
        /// </summary>
        /// <param name="createdBy">The user for whom the policy is being added. Cannot be null.</param>
        /// <param name="packageOwner">The owner of the package to which the policy applies. Cannot be null.</param>
        /// <param name="policyName">The name of the policy to be added. Must be a non-empty string.</param>
        /// <param name="policyType">The type of federated credential policy to be added.</param>
        /// <param name="criteria">The criteria that define the conditions under which the policy is applied. Must be a valid string.</param>
        Task<FederatedCredentialPolicyValidationResult> AddPolicyAsync(User createdBy, string packageOwner, string criteria, string? policyName, FederatedCredentialType policyType);

        /// <summary>
        /// Updates <see cref="FederatedCredentialPolicy">. Logs an audit record for the policy update.
        /// </summary>
        /// <param name="policy">The federated credential policy to be updated. Cannot be null.</param>
        /// <param name="criteria">The criteria used to determine how the policy should be updated. Cannot be null or empty.</param>
        /// <param name="policyName">The optional name of the policy. If provided, it will be used to identify the policy during the update
        /// process.</param>
        Task<FederatedCredentialPolicyValidationResult> UpdatePolicyAsync(FederatedCredentialPolicy policy, string criteria, string? policyName);

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
        /// Determines whether the specified user is a valid trusted publishing policy owner for the given package
        /// owner.
        /// </summary>
        /// <param name="user">The user to validate as a trusted publishing policy owner.</param>
        /// <param name="packageOwner">The owner of the package for which the policy is being validated.</param>
        /// <returns><see langword="true"/> if the specified user is a valid trusted publishing policy owner for the package
        /// owner; otherwise, <see langword="false"/>.</returns>
        bool IsValidPolicyOwner(User user, User packageOwner);

        /// <summary>
        /// Deletes a given federated credential policy and all associated API keys.
        /// </summary>
        /// <param name="policy">The policy to delete.</param>
        Task DeletePolicyAsync(FederatedCredentialPolicy policy);

        /// <summary>
        /// Gets policies created by a specific user.
        /// </summary>
        /// <param name="userKey">The key of the user who created the policies.</param>
        /// <returns>List of federated credential policies created by the user.</returns>
        IReadOnlyList<FederatedCredentialPolicy> GetPoliciesCreatedByUser(int userKey);

        /// <summary>
        /// Gets a policy by its key.
        /// </summary>
        /// <param name="policyKey">The key of the policy to retrieve.</param>
        /// <returns>The policy if found, otherwise null.</returns>
        FederatedCredentialPolicy? GetPolicyByKey(int policyKey);

        /// <summary>
        /// Gets policies related to specified user keys (either created by or owned by the users).
        /// </summary>
        /// <param name="userKeys">The list of user keys.</param>
        /// <returns>List of related federated credential policies.</returns>
        IReadOnlyList<FederatedCredentialPolicy> GetPoliciesRelatedToUserKeys(IReadOnlyList<int> userKeys);
    }

    public class FederatedCredentialService : IFederatedCredentialService
    {
        private readonly IUserService _userService;
        private readonly IFederatedCredentialRepository _repository;
        private readonly IFederatedCredentialPolicyEvaluator _evaluator;
        private readonly ICredentialBuilder _credentialBuilder;
        private readonly IAuthenticationService _authenticationService;
        private readonly IAuditingService _auditingService;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IFederatedCredentialConfiguration _configuration;
        private readonly IGalleryConfigurationService _galleryConfigurationService;

        public FederatedCredentialService(
            IUserService userService,
            IFederatedCredentialRepository repository,
            IFederatedCredentialPolicyEvaluator evaluator,
            ICredentialBuilder credentialBuilder,
            IAuthenticationService authenticationService,
            IAuditingService auditingService,
            IDateTimeProvider dateTimeProvider,
            IFederatedCredentialConfiguration configuration,
            IGalleryConfigurationService galleryConfigurationService)
        {
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
            _credentialBuilder = credentialBuilder ?? throw new ArgumentNullException(nameof(credentialBuilder));
            _authenticationService = authenticationService ?? throw new ArgumentNullException(nameof(authenticationService));
            _auditingService = auditingService ?? throw new ArgumentNullException(nameof(auditingService));
            _dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _galleryConfigurationService = galleryConfigurationService ?? throw new ArgumentNullException(nameof(configuration));
        }

        public async Task<FederatedCredentialPolicyValidationResult> AddPolicyAsync(User createdBy, string packageOwner, string criteria, string? policyName, FederatedCredentialType policyType)
        {
            // Currently we do not audit missing user or package owner. This falls into category of
            // basic user input validation. Such validation moslty lives in the Controllers. With
            // a small exception here (to share more code between controllers).
            if (createdBy == null)
            {
                return FederatedCredentialPolicyValidationResult.BadRequest(
                    $"The policy user is missing.",
                    nameof(FederatedCredentialPolicy.CreatedBy));
            }

            var policyPackageOwner = _userService.FindByUsername(packageOwner);
            if (policyPackageOwner is null)
            {
                return FederatedCredentialPolicyValidationResult.BadRequest(
                    $"The policy package owner '{packageOwner}' does not exist.",
                    nameof(FederatedCredentialPolicy.PackageOwner));
            }

            // From this point on we should audit all activities related to the policy.
            var policy = new FederatedCredentialPolicy
            {
                PolicyName = policyName,
                Created = _dateTimeProvider.UtcNow,
                CreatedBy = createdBy,
                PackageOwner = policyPackageOwner,
                Type = policyType,
                Criteria = criteria,
            };

            var result = await ValidatePolicyAsync(policy);
            if (result.Type != FederatedCredentialPolicyValidationResultType.Success)
            {
                return result;
            }

            await _repository.AddPolicyAsync(policy, saveChanges: true);
            await _auditingService.SaveAuditRecordAsync(FederatedCredentialPolicyAuditRecord.Create(policy));
            return FederatedCredentialPolicyValidationResult.Success(policy);
        }

        public async Task<FederatedCredentialPolicyValidationResult> UpdatePolicyAsync(FederatedCredentialPolicy policy, string criteria, string? policyName)
        {
            // Create temp policy to validate the update.
            FederatedCredentialPolicy tempPolicy = new FederatedCredentialPolicy
            {
                Key = policy.Key,
                CreatedBy = policy.CreatedBy,
                CreatedByUserKey = policy.CreatedByUserKey,
                PackageOwner = policy.PackageOwner,
                PackageOwnerUserKey = policy.PackageOwnerUserKey,
                Created = policy.Created,
                Type = policy.Type,

                // New values for the update.
                PolicyName = policyName,
                Criteria = criteria,
            };

            // From this point on we should audit all activities related to the policy.
            var result = await ValidatePolicyAsync(tempPolicy);
            if (result.Type != FederatedCredentialPolicyValidationResultType.Success)
            {
                return result;
            }

            // Skip update if nothing has changed. It is IMPORTANT to do the check after the validation.
            // It can update the criteria, e.g. ensure ValidateBy date for temporary GitHub Actions policies.
            if (string.Equals(policy.PolicyName, tempPolicy.PolicyName) && string.Equals(policy.Criteria, tempPolicy.Criteria))
            {
                return FederatedCredentialPolicyValidationResult.Success(policy);
            }

            // Delete all existing API keys created from this policy.
            var credentials = _repository.GetShortLivedApiKeysForPolicy(policy.Key);
            foreach (var credential in credentials)
            {
                await _authenticationService.RemoveCredential(policy.CreatedBy, credential, commitChanges: false);
            }

            // Update policy
            policy.PolicyName = tempPolicy.PolicyName;
            policy.Criteria = tempPolicy.Criteria;
            await _repository.SavePoliciesAsync();

            // Create audit record for the update.
            var auditRecord = FederatedCredentialPolicyAuditRecord.Update(policy, credentials);
            await _auditingService.SaveAuditRecordAsync(auditRecord);

            return FederatedCredentialPolicyValidationResult.Success(policy);
        }

        /// <summary>
        /// Validates <see cref="FederatedCredentialPolicy"/>. Logs an audit record in case of an unsuccessfull validation.
        /// </summary>
        private async Task<FederatedCredentialPolicyValidationResult> ValidatePolicyAsync(FederatedCredentialPolicy policy)
        {
            FederatedCredentialPolicyValidationResult result;
            if (!IsValidPolicyOwner(policy.CreatedBy, policy.PackageOwner))
            {
                result = FederatedCredentialPolicyValidationResult.Unauthorized(
                    $"The user '{policy.CreatedBy.Username}' does not have the required permissions to add a federated credential policy for package owner '{policy.PackageOwner.Username}'.",
                    nameof(FederatedCredentialPolicy.PackageOwner));
            }
            else
            {
                result = _evaluator.ValidatePolicy(policy);
            }

            switch (result.Type)
            {
                case FederatedCredentialPolicyValidationResultType.BadRequest:
                    await _auditingService.SaveAuditRecordAsync(FederatedCredentialPolicyAuditRecord.BadRequest(
                        policy, result.UserMessage!));
                    break;

                case FederatedCredentialPolicyValidationResultType.Unauthorized:
                    await _auditingService.SaveAuditRecordAsync(FederatedCredentialPolicyAuditRecord.Unauthorized(
                        policy, result.UserMessage!));
                    break;

                case FederatedCredentialPolicyValidationResultType.Success:
                    // No audit record for successful validation as callers need to do more work.
                    break;

                default:
                    throw new NotImplementedException($"Unexpected result type while processing invalid request: {result.Type}");
            }

            return result;
        }

        public bool IsValidPolicyOwner(User user, User packageOwner)
        {
            var testScope = new Scope(packageOwner, NuGetPackagePattern.AllInclusivePattern, NuGetScopes.All);
            return _credentialBuilder.VerifyScopes(user, [testScope]);
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

        public IReadOnlyList<FederatedCredentialPolicy> GetPoliciesCreatedByUser(int userKey)
            => _repository.GetPoliciesCreatedByUser(userKey);

        public FederatedCredentialPolicy? GetPolicyByKey(int policyKey)
            => _repository.GetPolicyByKey(policyKey);

        public IReadOnlyList<FederatedCredentialPolicy> GetPoliciesRelatedToUserKeys(IReadOnlyList<int> userKeys)
            => _repository.GetPoliciesRelatedToUserKeys(userKeys);


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
                case OidcTokenEvaluationResultType.NoMatchingPolicy:
                    if (policyEvaluation.UserError is string userError)
                    {
                        return GenerateApiKeyResult.Unauthorized(userError);
                    }
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

            var apiKeyCredential = _credentialBuilder.CreateShortLivedApiKey(
                _configuration.ShortLivedApiKeyDuration,
                policyEvaluation.MatchedPolicy,
                _galleryConfigurationService.Current.Environment,
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

            return null;
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
