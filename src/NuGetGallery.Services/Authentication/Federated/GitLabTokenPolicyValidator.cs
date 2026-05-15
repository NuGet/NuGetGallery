// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
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
		private const string ProjectPathClaim = "project_path";
		private const string RefClaim = "ref";
		private const string EnvironmentClaim = "environment";

		private readonly IFeatureFlagService _featureFlagService;

		public GitLabTokenPolicyValidator(
			ConfigurationManager<OpenIdConnectConfiguration> oidcConfigManager,
			IFederatedCredentialConfiguration configuration,
			IFeatureFlagService featureFlagService,
			JsonWebTokenHandler jsonWebTokenHandler)
			: base(oidcConfigManager, configuration, jsonWebTokenHandler)
		{
			_featureFlagService = featureFlagService ?? throw new ArgumentNullException(nameof(featureFlagService));
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

			// Strip "https://gitlab.com/" prefix if present
			const string httpsGitLabPrefix = "https://gitlab.com/";
			if (projectPath.StartsWith(httpsGitLabPrefix, StringComparison.OrdinalIgnoreCase))
			{
				projectPath = projectPath.Substring(httpsGitLabPrefix.Length);
			}

			projectPath = projectPath.TrimEnd('/');

			// If the user entered "my-group/my-project", strip the namespace prefix
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

		public override Task<FederatedCredentialPolicyResult> EvaluatePolicyAsync(FederatedCredentialPolicy policy, JsonWebToken jwt)
		{
			if (policy.Type != FederatedCredentialType.GitLabCI)
			{
				return Task.FromResult(FederatedCredentialPolicyResult.NotApplicable);
			}

			string? error = TryGetRequiredClaim(jwt, NamespacePathClaim, out _);
			if (error != null)
			{
				return Task.FromResult(FederatedCredentialPolicyResult.Unauthorized(error, isErrorDisclosable: true));
			}

			error = TryGetRequiredClaim(jwt, ProjectPathClaim, out _);
			if (error != null)
			{
				return Task.FromResult(FederatedCredentialPolicyResult.Unauthorized(error, isErrorDisclosable: true));
			}

			var criteria = GitLabCriteria.FromDatabaseJson(policy.Criteria);

			// Validate namespace_path claim
			error = ValidateClaimExactMatch(jwt, NamespacePathClaim, criteria.NamespacePath, StringComparison.OrdinalIgnoreCase);
			if (error != null)
			{
				return Task.FromResult(FederatedCredentialPolicyResult.Unauthorized(error));
			}

			// Validate project_path claim (GitLab's project_path is the full path: "namespace/project")
			string expectedProjectPath = $"{criteria.NamespacePath}/{criteria.ProjectPath}";
			error = ValidateClaimExactMatch(jwt, ProjectPathClaim, expectedProjectPath, StringComparison.OrdinalIgnoreCase);
			if (error != null)
			{
				return Task.FromResult(FederatedCredentialPolicyResult.Unauthorized(error));
			}

			// IMPORTANT. By now we validated namespace and project path.
			// From now on we can report errors as disclosable.

			// Validate ref if specified in criteria
			if (!string.IsNullOrWhiteSpace(criteria.Ref))
			{
				if (TryGetRequiredClaim(jwt, RefClaim, out string refValue) != null)
				{
					refValue = string.Empty;
				}

				if (!string.Equals(refValue, criteria.Ref, StringComparison.OrdinalIgnoreCase))
				{
					return Task.FromResult(FederatedCredentialPolicyResult.Unauthorized(
						$"Ref mismatch for policy '{policy.PolicyName}': expected '{criteria.Ref}', actual '{refValue}'",
						isErrorDisclosable: true));
				}
			}

			// Validate environment if specified in criteria
			if (!string.IsNullOrWhiteSpace(criteria.Environment))
			{
				if (TryGetRequiredClaim(jwt, EnvironmentClaim, out string environment) != null)
				{
					environment = string.Empty;
				}

				if (!string.Equals(environment, criteria.Environment, StringComparison.OrdinalIgnoreCase))
				{
					return Task.FromResult(FederatedCredentialPolicyResult.Unauthorized(
						$"Environment mismatch for policy '{policy.PolicyName}': expected '{criteria.Environment}', actual '{environment}'",
						isErrorDisclosable: true));
				}
			}

			return Task.FromResult(FederatedCredentialPolicyResult.Success);
		}
	}
}
