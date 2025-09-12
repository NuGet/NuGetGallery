// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.Entity.Infrastructure;
using System.IO;
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

        private readonly IFederatedCredentialRepository _federatedCredentialRepository;
        private readonly IAuditingService _auditingService;
        private readonly IFeatureFlagService _featureFlagService;

        public GitHubTokenPolicyValidator(
            IFederatedCredentialRepository federatedCredentialRepository,
            ConfigurationManager<OpenIdConnectConfiguration> oidcConfigManager,
            IFederatedCredentialConfiguration configuration,
            IAuditingService auditingService,
            IFeatureFlagService featureFlagService,
            JsonWebTokenHandler jsonWebTokenHandler)
            : base(oidcConfigManager, configuration, jsonWebTokenHandler)
        {
            _federatedCredentialRepository = federatedCredentialRepository ?? throw new ArgumentNullException(nameof(federatedCredentialRepository));
            _auditingService = auditingService ?? throw new ArgumentNullException(nameof(auditingService));
            _featureFlagService = featureFlagService ?? throw new ArgumentNullException(nameof(featureFlagService));
        }

        public override string IssuerAuthority => Authority;
        public override FederatedCredentialIssuerType IssuerType => FederatedCredentialIssuerType.GitHubActions;

        public override FederatedCredentialPolicyValidationResult ValidatePolicy(FederatedCredentialPolicy policy)
        {
            if (policy.Type != FederatedCredentialType.GitHubActions)
            {
                // We do not expect callers to pass non-GitHub policies to this validator.
                return FederatedCredentialPolicyValidationResult.BadRequest(
                    $"Invalid policy type '{policy.Type}' for GitHub Actions validation",
                    policyPropertyName: null);
            }

            if (!_featureFlagService.IsTrustedPublishingEnabled(policy.CreatedBy))
            {
                return FederatedCredentialPolicyValidationResult.BadRequest(
                    $"Trusted Publishing is not enabled for '{policy.CreatedBy.Username}'.",
                    nameof(FederatedCredentialPolicy.CreatedBy));
            }

            // Ensure consistent ValidateByDate. Note that for temporary GitHub Actions policies
            // we always reset it on each update (to 7 days from now). 
            GitHubCriteria gitHubCriteria = GitHubCriteria.FromDatabaseJson(policy.Criteria);
            gitHubCriteria.InitializeValidateByDate();
            policy.Criteria = gitHubCriteria.ToDatabaseJson();

            if (gitHubCriteria.Validate() is string error)
            {
                return FederatedCredentialPolicyValidationResult.BadRequest(error,
                    nameof(FederatedCredentialPolicy.Criteria));
            }

            // The policy name isn't required for OIDC token processing, but it's helpful for customers to identify
            // policies. The user-facing UI is expected to block policy creation or updates if the name is missing.
            // Here, we enforce the presence of a policy name when the request originates from the admin UI.
            // Users can still modify the name later if needed.
            if (string.IsNullOrWhiteSpace(policy.PolicyName))
            {
                // Use workflow file name as the policy name, e.g. , "prod" for "deployments/prod.yml".
                policy.PolicyName = Path.GetFileNameWithoutExtension(gitHubCriteria.WorkflowFile);
                if (policy.PolicyName.Length > FederatedCredentialPolicy.MaxPolicyNameLength)
                {
                    // If the policy name is too long, truncate it to the maximum length.
                    policy.PolicyName = policy.PolicyName[..FederatedCredentialPolicy.MaxPolicyNameLength];
                }
            }

            return base.ValidatePolicy(policy);
        }

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

            if (!_featureFlagService.IsTrustedPublishingEnabled(policy.CreatedBy))
            {
                return FederatedCredentialPolicyResult.Unauthorized($"Trusted publishing is not enabled for {policy.CreatedBy.Username}");
            }

            var criteria = GitHubCriteria.FromDatabaseJson(policy.Criteria);

            // Validate required GitHub criterias first
            string? error = ValidateClaimExactMatch(jwt, RepositoryOwnerClaim, criteria.RepositoryOwner, StringComparison.OrdinalIgnoreCase);
            if (error != null)
            {
                return FederatedCredentialPolicyResult.Unauthorized(error);
            }

            error = ValidateClaimExactMatch(jwt, RepositoryClaim, $"{criteria.RepositoryOwner}/{criteria.Repository}", StringComparison.OrdinalIgnoreCase);
            if (error != null)
            {
                return FederatedCredentialPolicyResult.Unauthorized(error);
            }

            error = ValidateClaimStartsWith(jwt, JobWorkflowRefClaim, $"{criteria.RepositoryOwner}/{criteria.Repository}/.github/workflows/{criteria.WorkflowFile}@", StringComparison.OrdinalIgnoreCase);
            if (error != null)
            {
                return FederatedCredentialPolicyResult.Unauthorized(error);
            }

            // Validate environment if specified in criteria. We do it last to make sure we report disclosable error
            if (!string.IsNullOrWhiteSpace(criteria.Environment))
            {
                error = ValidateClaimExactMatch(jwt, EnvironmentClaim, criteria.Environment!, StringComparison.OrdinalIgnoreCase);
                if (error != null)
                {
                    return FederatedCredentialPolicyResult.Unauthorized(error);
                }
            }

            // Check if this policy has expired (only applies to non-permanently enabled policies)
            if (!criteria.IsPermanentlyEnabled)
            {
                // // First time use. Check if policy is still enabled.
                if (!criteria.ValidateByDate.HasValue || DateTimeOffset.UtcNow > criteria.ValidateByDate.Value)
                {
                    return FederatedCredentialPolicyResult.Unauthorized(
                        $"The policy '{policy.PolicyName}' has expired. Sign in and renew the trust policy on the Trusted Publishing page.",
                        isErrorDisclosable: true);
                }

                // Get repo and owner IDs from the token
                error = TryGetRequiredClaim(jwt, RepositoryOwnerIdClaim, out string repositoryOwnerId);
                if (error != null)
                {
                    return FederatedCredentialPolicyResult.Unauthorized(error);
                }

                error = TryGetRequiredClaim(jwt, RepositoryIdClaim, out string repositoryId);
                if (error != null)
                {
                    return FederatedCredentialPolicyResult.Unauthorized(error);
                }

                // Update the policy with the repo and owner IDs
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
                        return FederatedCredentialPolicyResult.Unauthorized("The policy was not found after concurrent first use.");
                    }
                    var updatedCriteria = GitHubCriteria.FromDatabaseJson(updatedPolicy.Criteria);
                    if (!string.Equals(updatedCriteria.RepositoryOwnerId, criteria.RepositoryOwnerId, StringComparison.Ordinal) ||
                        !string.Equals(updatedCriteria.RepositoryId, criteria.RepositoryId, StringComparison.Ordinal))
                    {
                        return FederatedCredentialPolicyResult.Unauthorized($"The policy was updated with different repository owner/repo IDs during concurrent first use. Expected {criteria.RepositoryOwnerId}/{criteria.RepositoryId}, actual {updatedCriteria.RepositoryOwnerId}/{updatedCriteria.RepositoryId}");
                    }
                }
            }
            else
            {
                // Note that ID comparisons are case-sensitive
                error = ValidateClaimExactMatch(jwt, RepositoryOwnerIdClaim, criteria.RepositoryOwnerId!, StringComparison.Ordinal);
                if (error != null)
                {
                    return FederatedCredentialPolicyResult.Unauthorized(error);
                }

                error = ValidateClaimExactMatch(jwt, RepositoryIdClaim, criteria.RepositoryId!, StringComparison.Ordinal);
                if (error != null)
                {
                    return FederatedCredentialPolicyResult.Unauthorized(error);
                }
            }

            return FederatedCredentialPolicyResult.Success;
        }
    }
}
