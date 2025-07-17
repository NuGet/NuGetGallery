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

        private readonly IFederatedCredentialRepository _federatedCredentialRepository;

        public GitHubTokenPolicyValidator(
            IFederatedCredentialRepository federatedCredentialRepository,
            ConfigurationManager<OpenIdConnectConfiguration> oidcConfigManager,
            JsonWebTokenHandler jsonWebTokenHandler)
            : base(oidcConfigManager, jsonWebTokenHandler)
        {
            _federatedCredentialRepository = federatedCredentialRepository ?? throw new ArgumentNullException(nameof(federatedCredentialRepository));
        }

        public override string IssuerAuthority => Authority;
        public override FederatedCredentialIssuerType IssuerType => FederatedCredentialIssuerType.GitHubActions;

        public override async Task<TokenValidationResult> ValidateTokenAsync(JsonWebToken jwt)
        {
            var validationParameters = new TokenValidationParameters
            {
                ValidIssuer = Issuer,
                ValidAudience = ServicesConstants.NuGetAudience,
                ConfigurationManager = _oidcConfigManager,
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
                {
                    return FederatedCredentialPolicyResult.Unauthorized(error);
                }
            }

            return FederatedCredentialPolicyResult.Success;
        }

        /// <summary>
        /// Evaluates a GitHub Actions policy against a provided JSON Web Token (JWT).
        /// </summary>
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

            // Validate repository owner
            if (!jwt.TryGetPayloadValue<string>("repository_owner", out var repositoryOwner) ||
                string.IsNullOrWhiteSpace(repositoryOwner))
            {
                return "The JSON web token must have 'repository_owner' claim.";
            }

            if (!repositoryOwner.Equals(criteria.RepositoryOwner, StringComparison.OrdinalIgnoreCase))
            {
                return $"The JSON web token repository_owner claim '{repositoryOwner}' does not match policy owner '{criteria.RepositoryOwner}'.";
            }

            // Validate repository which contains both owner and repository name
            if (!jwt.TryGetPayloadValue<string>("repository", out var repository) ||
                string.IsNullOrWhiteSpace(repository))
            {
                return "The JSON web token must have 'repository' claim.";
            }

            var expectedRepository = $"{criteria.RepositoryOwner}/{criteria.Repository}";
            if (!repository.Equals(expectedRepository, StringComparison.OrdinalIgnoreCase))
            {
                return $"The JSON web token repository claim '{repository}' does not match policy repository '{expectedRepository}'.";
            }

            // Validate workflow file
            if (!jwt.TryGetPayloadValue<string>("job_workflow_ref", out var jobWorkflowRef) ||
                string.IsNullOrWhiteSpace(jobWorkflowRef))
            {
                return "The JSON web token must have 'job_workflow_ref' claim.";
            }

            // job_workflow_ref format: owner/repo/.github/workflows/workflow.yml@ref
            var expectedWorkflowRef = $"{criteria.RepositoryOwner}/{criteria.Repository}/.github/workflows/{criteria.WorkflowFile}@";
            if (!jobWorkflowRef.StartsWith(expectedWorkflowRef, StringComparison.OrdinalIgnoreCase))
            {
                return $"The JSON web token job_workflow_ref claim '{jobWorkflowRef}' does not match policy workflow file '{expectedWorkflowRef}'.";
            }

            // Validate environment if specified in criteria
            if (!string.IsNullOrWhiteSpace(criteria.Environment))
            {
                if (!jwt.TryGetPayloadValue<string>("environment", out var environment) ||
                    string.IsNullOrWhiteSpace(environment))
                {
                    return "The JSON web token must have 'environment' claim.";
                }

                if (!environment.Equals(criteria.Environment, StringComparison.OrdinalIgnoreCase))
                {
                    return $"The JSON web token environment claim '{environment}' does not match policy environment '{criteria.Environment}'.";
                }
            }

            // Get owner and repo IDs from token
            if (!jwt.TryGetPayloadValue<string>("repository_owner_id", out var repositoryOwnerId) ||
                string.IsNullOrWhiteSpace(repositoryOwnerId))
            {
                return "The JSON web token must have 'repository_owner_id' claim.";
            }
            if (!jwt.TryGetPayloadValue<string>("repository_id", out var repositoryId) ||
                string.IsNullOrWhiteSpace(repositoryId))
            {
                return "The JSON web token must have 'repository_id' claim.";
            }

            if (!criteria.IsPermanentlyEnabled)
            {
                // First time use, i.e. if policy is missing repo and owner IDs then get them from the token
                criteria.RepositoryOwnerId = repositoryOwnerId;
                criteria.RepositoryId = repositoryId;
                criteria.ValidateByDate = null;
                policy.Criteria = criteria.ToDatabaseJson();
                try
                {
                    await _federatedCredentialRepository.SavePoliciesAsync();
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
                if (!repositoryOwnerId.Equals(criteria.RepositoryOwnerId, StringComparison.Ordinal))
                {
                    return $"The JSON web token repository_owner_id claim '{repositoryOwnerId}' does not match policy owner id '{criteria.RepositoryOwnerId}'.";
                }
                if (!repositoryId.Equals(criteria.RepositoryId, StringComparison.Ordinal))
                {
                    return $"The JSON web token repository_id claim '{repositoryId}' does not match policy repository id '{criteria.RepositoryId}'.";
                }
            }

            return null;
        }
    }
}
