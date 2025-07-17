// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.Entity.Infrastructure;
using System.Threading.Tasks;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using NuGet.Services.Entities;
using NuGetGallery.Auditing;

#nullable enable

namespace NuGetGallery.Services.Authentication
{
    /// <summary>
    /// Validates GitHub Actions OpenID Connect (OIDC) tokens and evaluates federated credential policies
    /// for GitHub Actions-based trusted publishing.
    /// </summary>
    /// <remarks>
    /// See: https://docs.github.com/en/actions/concepts/security/openid-connect
    /// </remarks>
    public class GitHubTokenPolicyValidator : TokenPolicyValidator
    {
        public const string Authority = "token.actions.githubusercontent.com";
        public const string Issuer = $"https://{Authority}";
        public const string MetadataAddress = $"{Issuer}/.well-known/openid-configuration";

        private const string RepositoryOwnerClaim = "repository_owner";
        private const string RepositoryClaim = "repository";
        private const string JobWorkflowRefClaim = "job_workflow_ref";
        private const string EnvironmentClaim = "environment";
        private const string RepositoryOwnerIdClaim = "repository_owner_id";
        private const string RepositoryIdClaim = "repository_id";

        // Error message templates
        private const string MissingClaimError = "The JSON web token must have '{0}' claim.";
        private const string ClaimMismatchError = "The JSON web token {0} claim '{1}' does not match policy '{2}'.";

        private readonly IFederatedCredentialRepository _federatedCredentialRepository;
        private readonly IAuditingService _auditingService;

        public GitHubTokenPolicyValidator(
            IFederatedCredentialRepository federatedCredentialRepository,
            ConfigurationManager<OpenIdConnectConfiguration> oidcConfigManager,
            IFederatedCredentialConfiguration configuration,
            IAuditingService auditingService,
            JsonWebTokenHandler jsonWebTokenHandler)
            : base(oidcConfigManager, configuration, jsonWebTokenHandler)
        {
            _federatedCredentialRepository = federatedCredentialRepository ?? throw new ArgumentNullException(nameof(federatedCredentialRepository));
            _auditingService = auditingService ?? throw new ArgumentNullException(nameof(auditingService));
        }

        public override string IssuerAuthority => Authority;
        public override FederatedCredentialIssuerType IssuerType => FederatedCredentialIssuerType.GitHubActions;

        public override async Task<TokenValidationResult> ValidateTokenAsync(JsonWebToken jwt)
        {
            if (string.IsNullOrWhiteSpace(_configuration.NuGetAudience))
            {
                throw new InvalidOperationException("Unable to validate GitHub Actions token. NuGet audience is not configured.");
            }

            var validationParameters = new TokenValidationParameters
            {
                ValidIssuer = Issuer,
                ValidAudience = _configuration.NuGetAudience,
                ConfigurationManager = _oidcConfigManager,

                ValidateLifetime = true,
                RequireExpirationTime = true,
                ValidateIssuerSigningKey = true,
                RequireSignedTokens = true,
            };

            var result = await _jsonWebTokenHandler.ValidateTokenAsync(jwt, validationParameters);
            return result;
        }

        public override async Task<FederatedCredentialPolicyResult> EvaluatePolicyAsync(FederatedCredentialPolicy policy, JsonWebToken jwt)
        {
            if (policy.Type != FederatedCredentialType.GitHubActions)
            {
                return FederatedCredentialPolicyResult.NotApplicable;
            }

            string? error = await EvaluateGitHubActionsPolicyAsync(policy, jwt);
            if (error != null)
            {
                // If the policy is not valid, we return an Unauthorized result with the error message.
                // This indicates that the token does not meet the criteria defined in the policy.
                return FederatedCredentialPolicyResult.Unauthorized(error);
            }

            return FederatedCredentialPolicyResult.Success;
        }

        /// <summary>
        /// Evaluates a GitHub Actions federated credential policy against a provided JSON Web Token (JWT)
        /// to determine if the token's claims match the policy criteria.
        /// </summary>
        /// <returns>
        /// <see langword="null"/> if the policy evaluation succeeds; otherwise, an error message describing the validation failure.
        /// </returns>
        private async Task<string?> EvaluateGitHubActionsPolicyAsync(FederatedCredentialPolicy policy, JsonWebToken jwt)
        {
            var criteria = GitHubCriteria.FromDatabaseJson(policy.Criteria);

            // Check if this policy has expired (only applies to non-permanently enabled policies)
            if (!criteria.IsPermanentlyEnabled)
            {
                if (!criteria.ValidateByDate.HasValue || DateTimeOffset.UtcNow > criteria.ValidateByDate.Value)
                {
                    return "The policy has expired.";
                }
            }

            string? error;
            error = ValidateClaim(jwt, RepositoryOwnerClaim, criteria.RepositoryOwner);
            if (error != null)
            {
                return error;
            }

            error = ValidateClaim(jwt, RepositoryClaim, $"{criteria.RepositoryOwner}/{criteria.Repository}");
            if (error != null)
            {
                return error;
            }

            error = ValidateClaim(jwt, JobWorkflowRefClaim, $"{criteria.RepositoryOwner}/{criteria.Repository}/.github/workflows/{criteria.WorkflowFile}@", fullValue: false);
            if (error != null)
            {
                return error;
            }

            // Validate environment if specified in criteria
            if (!string.IsNullOrWhiteSpace(criteria.Environment))
            {
                error = ValidateClaim(jwt, EnvironmentClaim, criteria.Environment!);
                if (error != null)
                {
                    return error;
                }
            }

            if (!criteria.IsPermanentlyEnabled)
            {
                // First time use, i.e. if policy is missing repo and owner IDs then get them from the token
                error = TryGetRequiredClaim(jwt, RepositoryOwnerIdClaim, out string repositoryOwnerId);
                if (error != null)
                {
                    return error;
                }

                error = TryGetRequiredClaim(jwt, RepositoryIdClaim, out string repositoryId);
                if (error != null)
                {
                    return error;
                }

                criteria.RepositoryOwnerId = repositoryOwnerId;
                criteria.RepositoryId = repositoryId;
                criteria.ValidateByDate = null;
                policy.Criteria = criteria.ToDatabaseJson();
                try
                {
                    await _federatedCredentialRepository.SavePoliciesAsync();
                    await _auditingService.SaveAuditRecordAsync(FederatedCredentialPolicyAuditRecord.FirstUseUpdate(policy));
                }
                catch (DbUpdateConcurrencyException)
                {
                    // Highly unlikely, but a concurrent "first use" scenario is possible.
                    // This can only occur if *all* of the following conditions are met:
                    //   1. The policy has never previously been used to match an incoming GitHub Actions OIDC token.
                    //   2. The same workflow (e.g., github.com/contoso/repo/.github/workflows/prod.yml)
                    //      runs concurrently and both instances call nuget.org at the same time.
                    // Verify that updated policy has same IDs.
                    var updatedPolicy = _federatedCredentialRepository.GetPolicyByKey(policy.Key);
                    if (updatedPolicy == null)
                    {
                        return "The policy was not found after concurrent first use.";
                    }
                    var updatedCriteria = GitHubCriteria.FromDatabaseJson(updatedPolicy.Criteria);
                    if (!string.Equals(updatedCriteria.RepositoryOwnerId, criteria.RepositoryOwnerId, StringComparison.Ordinal) ||
                        !string.Equals(updatedCriteria.RepositoryId, criteria.RepositoryId, StringComparison.Ordinal))
                    {
                        return $"The policy was updated with different repository owner/repo IDs during concurrent first use. Expected {criteria.RepositoryOwnerId}/{criteria.RepositoryId}, actual {updatedCriteria.RepositoryOwnerId}/{updatedCriteria.RepositoryId}";
                    }
                }
            }
            else
            {
                // Note that ID comparisons are case-sensitive
                error = ValidateClaim(jwt, RepositoryOwnerIdClaim, criteria.RepositoryOwnerId!, StringComparison.Ordinal);
                if (error != null)
                {
                    return error;
                }
                error = ValidateClaim(jwt, RepositoryIdClaim, criteria.RepositoryId!, StringComparison.Ordinal);
                if (error != null)
                {
                    return error;
                }
            }

            return null;
        }

        /// <summary>
        /// Attempts to get a required string claim from the JWT token.
        /// </summary>
        /// <returns><see langword="null"/> if the claim value present; otherwise, an error message.</returns>
        private static string? TryGetRequiredClaim(JsonWebToken jwt, string claim, out string claimValue)
        {
            if (!jwt.TryGetPayloadValue(claim, out claimValue) || string.IsNullOrWhiteSpace(claimValue))
            {
                return string.Format(MissingClaimError, claim);
            }

            return null;
        }

        /// <summary>
        /// Validates that a JWT claim exists and equals the expected value using the specified string comparison.
        /// </summary>
        /// <returns><see langword="null"/> if the claim is valid; otherwise, an error message.</returns>
        private static string? ValidateClaim(JsonWebToken jwt, string claim, string expectedValue, StringComparison comparison = StringComparison.OrdinalIgnoreCase, bool fullValue = true)
        {
            if (TryGetRequiredClaim(jwt, claim, out string claimValue) is string error)
            {
                return error;
            }

            var isValid = fullValue
                ? claimValue.Equals(expectedValue, comparison)
                : claimValue.StartsWith(expectedValue, comparison);
            if (!isValid)
            {
                return string.Format(ClaimMismatchError, claim, claimValue, expectedValue);
            }

            return null;
        }
    }
}
