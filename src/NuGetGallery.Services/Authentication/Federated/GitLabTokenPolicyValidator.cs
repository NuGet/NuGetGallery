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
    /// Validates GitLab CI/CD OpenID Connect (OIDC) tokens and evaluates federated credential policies
    /// for GitLab-based trusted publishing.
    /// </summary>
    /// <remarks>
    /// See: https://docs.gitlab.com/ee/ci/secrets/id_token_authentication.html
    /// </remarks>
    public class GitLabTokenPolicyValidator : TokenPolicyValidator
    {
        public const string Authority = "gitlab.com";
        public const string Issuer = $"https://{Authority}";
        public const string MetadataAddress = $"{Issuer}/.well-known/openid-configuration";

        private const string NamespacePathClaim = "namespace_path";
        private const string NamespaceIdClaim = "namespace_id";
        private const string ProjectPathClaim = "project_path";
        private const string ProjectIdClaim = "project_id";
        private const string RefClaim = "ref";
        private const string RefTypeClaim = "ref_type";
        private const string EnvironmentClaim = "environment";

        private readonly IFederatedCredentialRepository _federatedCredentialRepository;
        private readonly IAuditingService _auditingService;

        public GitLabTokenPolicyValidator(
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
        public override FederatedCredentialIssuerType IssuerType => FederatedCredentialIssuerType.GitLabCI;

        public override FederatedCredentialPolicyValidationResult ValidatePolicy(FederatedCredentialPolicy policy)
        {
            if (policy.Type != FederatedCredentialType.GitLabCI)
            {
                return FederatedCredentialPolicyValidationResult.BadRequest(
                    $"Invalid policy type '{policy.Type}' for GitLab CI/CD validation",
                    policyPropertyName: null);
            }

            GitLabCriteria criteria = GitLabCriteria.FromDatabaseJson(policy.Criteria);
            NormalizeProjectPath(criteria);
            criteria.InitializeValidateByDate();
            policy.Criteria = criteria.ToDatabaseJson();

            if (criteria.Validate() is string error)
            {
                return FederatedCredentialPolicyValidationResult.BadRequest(error,
                    nameof(FederatedCredentialPolicy.Criteria));
            }

            if (string.IsNullOrWhiteSpace(policy.PolicyName))
            {
                policy.PolicyName = criteria.ProjectPath;
                if (policy.PolicyName.Length > FederatedCredentialPolicy.MaxPolicyNameLength)
                {
                    policy.PolicyName = policy.PolicyName[..FederatedCredentialPolicy.MaxPolicyNameLength];
                }
            }

            return base.ValidatePolicy(policy);
        }

        /// <summary>
        /// Normalizes the project path by stripping a leading URL or namespace prefix.
        /// Users may enter "https://gitlab.com/my-group/my-project" or "my-group/my-project"
        /// instead of just "my-project".
        /// </summary>
        private static void NormalizeProjectPath(GitLabCriteria criteria)
        {
            string projectPath = criteria.ProjectPath;

            const string httpsGitLabPrefix = "https://gitlab.com/";
            if (projectPath.StartsWith(httpsGitLabPrefix, StringComparison.OrdinalIgnoreCase))
            {
                projectPath = projectPath.Substring(httpsGitLabPrefix.Length);
            }

            projectPath = projectPath.TrimEnd('/');

            if (projectPath.Contains("/") && !string.IsNullOrEmpty(criteria.NamespacePath))
            {
                string expectedPrefix = criteria.NamespacePath + "/";
                if (projectPath.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    projectPath = projectPath.Substring(expectedPrefix.Length);
                }
            }

            criteria.ProjectPath = projectPath;
        }

        public override async Task<TokenValidationResult> ValidateTokenAsync(JsonWebToken jwt)
        {
            if (string.IsNullOrWhiteSpace(_configuration.NuGetAudience))
            {
                throw new InvalidOperationException("Unable to validate GitLab CI/CD token. NuGet audience is not configured.");
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
            if (policy.Type != FederatedCredentialType.GitLabCI)
            {
                return FederatedCredentialPolicyResult.NotApplicable;
            }

            // Check for required claims
            string? error = TryGetRequiredClaim(jwt, NamespacePathClaim, out _);
            if (error != null)
            {
                return FederatedCredentialPolicyResult.Unauthorized(error, isErrorDisclosable: true);
            }

            error = TryGetRequiredClaim(jwt, ProjectPathClaim, out _);
            if (error != null)
            {
                return FederatedCredentialPolicyResult.Unauthorized(error, isErrorDisclosable: true);
            }

            error = TryGetRequiredClaim(jwt, NamespaceIdClaim, out string namespaceId);
            if (error != null)
            {
                return FederatedCredentialPolicyResult.Unauthorized(error, isErrorDisclosable: true);
            }

            error = TryGetRequiredClaim(jwt, ProjectIdClaim, out string projectId);
            if (error != null)
            {
                return FederatedCredentialPolicyResult.Unauthorized(error, isErrorDisclosable: true);
            }

            var criteria = GitLabCriteria.FromDatabaseJson(policy.Criteria);

            // Validate namespace_path claim
            error = ValidateClaimExactMatch(jwt, NamespacePathClaim, criteria.NamespacePath, StringComparison.OrdinalIgnoreCase);
            if (error != null)
            {
                return FederatedCredentialPolicyResult.Unauthorized(error);
            }

            // Validate project_path claim (GitLab's project_path is the full path: "namespace/project")
            string expectedProjectPath = $"{criteria.NamespacePath}/{criteria.ProjectPath}";
            error = ValidateClaimExactMatch(jwt, ProjectPathClaim, expectedProjectPath, StringComparison.OrdinalIgnoreCase);
            if (error != null)
            {
                return FederatedCredentialPolicyResult.Unauthorized(error);
            }

            // TOFU: on first use, capture the numeric namespace and project IDs
            if (!criteria.IsPermanentlyEnabled)
            {
                if (!criteria.ValidateByDate.HasValue || DateTimeOffset.UtcNow > criteria.ValidateByDate.Value)
                {
                    return FederatedCredentialPolicyResult.Unauthorized(
                        $"The policy '{policy.PolicyName}' has expired. Sign in and renew the trust policy on the Trusted Publishing page.",
                        isErrorDisclosable: true);
                }

                // First use: lock down the policy to the numeric IDs from this token
                criteria.NamespaceId = namespaceId;
                criteria.ProjectId = projectId;
                criteria.ValidateByDate = null;
                policy.Criteria = criteria.ToDatabaseJson();
                try
                {
                    await _federatedCredentialRepository.SavePoliciesAsync();
                    await _auditingService.SaveAuditRecordAsync(FederatedCredentialPolicyAuditRecord.FirstUseUpdate(policy));
                }
                catch (DbUpdateConcurrencyException)
                {
                    // Concurrent first-use scenario: re-read and verify both instances captured the same IDs.
                    var updatedPolicy = _federatedCredentialRepository.GetPolicyByKey(policy.Key);
                    if (updatedPolicy == null)
                    {
                        return FederatedCredentialPolicyResult.Unauthorized("The policy was not found after concurrent first use.");
                    }

                    var updatedCriteria = GitLabCriteria.FromDatabaseJson(updatedPolicy.Criteria);
                    if (!string.Equals(updatedCriteria.NamespaceId, criteria.NamespaceId, StringComparison.Ordinal) ||
                        !string.Equals(updatedCriteria.ProjectId, criteria.ProjectId, StringComparison.Ordinal))
                    {
                        return FederatedCredentialPolicyResult.Unauthorized(
                            $"The policy was updated with different namespace/project IDs during concurrent first use. " +
                            $"Expected {criteria.NamespaceId}/{criteria.ProjectId}, actual {updatedCriteria.NamespaceId}/{updatedCriteria.ProjectId}");
                    }
                }
            }
            else
            {
                // Subsequent uses: validate against the locked-in numeric IDs
                error = ValidateClaimExactMatch(jwt, NamespaceIdClaim, criteria.NamespaceId!, StringComparison.Ordinal);
                if (error != null)
                {
                    return FederatedCredentialPolicyResult.Unauthorized(error);
                }

                error = ValidateClaimExactMatch(jwt, ProjectIdClaim, criteria.ProjectId!, StringComparison.Ordinal);
                if (error != null)
                {
                    return FederatedCredentialPolicyResult.Unauthorized(error);
                }
            }

            // IMPORTANT. By now we validated namespace and project path including IDs.
            // From now on we can report errors as disclosable.

            // Validate ref if specified in criteria
            if (!string.IsNullOrWhiteSpace(criteria.Ref))
            {
                if (TryGetRequiredClaim(jwt, RefClaim, out string refValue) != null)
                {
                    refValue = string.Empty;
                }

                if (!string.Equals(refValue, criteria.Ref, StringComparison.Ordinal))
                {
                    return FederatedCredentialPolicyResult.Unauthorized(
                        $"Ref mismatch for policy '{policy.PolicyName}': expected '{criteria.Ref}', actual '{refValue}'",
                        isErrorDisclosable: true);
                }

                // Require the ref to be a branch. Tags with the same name as a branch would
                // otherwise satisfy the policy. Only branch-based publishing is supported.
                if (TryGetRequiredClaim(jwt, RefTypeClaim, out string refType) != null ||
                    !string.Equals(refType, "branch", StringComparison.OrdinalIgnoreCase))
                {
                    return FederatedCredentialPolicyResult.Unauthorized(
                        $"Ref '{refValue}' for policy '{policy.PolicyName}' must be a branch.",
                        isErrorDisclosable: true);
                }
            }

            // Validate environment if specified in criteria
            if (!string.IsNullOrWhiteSpace(criteria.Environment))
            {
                if (TryGetRequiredClaim(jwt, EnvironmentClaim, out string environment) != null)
                {
                    environment = string.Empty;
                }

                if (!string.Equals(environment, criteria.Environment, StringComparison.Ordinal))
                {
                    return FederatedCredentialPolicyResult.Unauthorized(
                        $"Environment mismatch for policy '{policy.PolicyName}': expected '{criteria.Environment}', actual '{environment}'",
                        isErrorDisclosable: true);
                }
            }

            return FederatedCredentialPolicyResult.Success;
        }
    }
}
