// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Identity.Web;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Microsoft.IdentityModel.Validators;
using NuGet.Services.Entities;

#nullable enable

namespace NuGetGallery.Services.Authentication
{
    public class EntraIdTokenPolicyValidator : TokenPolicyValidator
    {
        public const string Authority = "login.microsoftonline.com";
        public const string Issuer = $"https://{Authority}/common/v2.0";
        public const string MetadataAddress = $"{Issuer}/.well-known/openid-configuration";
        public const string AzureDevOpsAttributeNamespace = "rISbSSETf0KqFyZ8ppdXmA";

        private const string IdentityTypeClaim = "idtyp";
        private const string FederatedManagedIdentityType = "fmi";
        private const string AzurePipelinesAttributesClaim = "xms_attr";
        private const string FederatedManagedIdentitySubjectPattern =
            "^/eid1/c/[^/\\s]+/t/[^/\\s]+/a/[^/\\s]+/pl/h/[^/\\s]+/d/[^/\\s]+$";

        private static readonly Regex FederatedManagedIdentitySubjectRegex =
            RegexEx.CreateWithTimeout(FederatedManagedIdentitySubjectPattern, RegexOptions.CultureInvariant);

        private readonly IFeatureFlagService _featureFlagService;

        public EntraIdTokenPolicyValidator(
            ConfigurationManager<OpenIdConnectConfiguration> oidcConfigManager,
            IFederatedCredentialConfiguration configuration,
            IFeatureFlagService featureFlagService,
            JsonWebTokenHandler jsonWebTokenHandler)
            : base(oidcConfigManager, configuration, jsonWebTokenHandler, "uti")
        {
            _featureFlagService = featureFlagService ?? throw new ArgumentNullException(nameof(featureFlagService));
        }

        public override string IssuerAuthority => Authority;
        public override FederatedCredentialIssuerType IssuerType => FederatedCredentialIssuerType.EntraId;

        public override FederatedCredentialPolicyValidationResult ValidatePolicy(FederatedCredentialPolicy policy)
        {
            if (policy.Type != FederatedCredentialType.EntraIdServicePrincipal
                && policy.Type != FederatedCredentialType.AzurePipelines)
            {
                return FederatedCredentialPolicyValidationResult.BadRequest(
                    $"Invalid policy type '{policy.Type}' for Entra ID validation.",
                    policyPropertyName: null);
            }

            if (!_featureFlagService.CanUseFederatedCredentials(policy.PackageOwner))
            {
                return FederatedCredentialPolicyValidationResult.BadRequest(
                    $"The package owner '{policy.PackageOwner.Username}' is not enabled to use federated credentials.",
                    nameof(FederatedCredentialPolicy.PackageOwner));
            }

            if (string.IsNullOrWhiteSpace(policy.Criteria))
            {
                return FederatedCredentialPolicyValidationResult.BadRequest(
                    $"Criteria must be provided for {GetPolicyTypeDisplayName(policy.Type)} policies.",
                    nameof(FederatedCredentialPolicy.Criteria));
            }

            if (policy.Type == FederatedCredentialType.AzurePipelines)
            {
                var criteria = AzurePipelinesCriteria.FromDatabaseJson(policy.Criteria);
                policy.Criteria = criteria.ToDatabaseJson();

                if (criteria.Validate() is string error)
                {
                    return FederatedCredentialPolicyValidationResult.BadRequest(
                        error,
                        nameof(FederatedCredentialPolicy.Criteria));
                }
            }
            else
            {
                var criteria = JsonSerializer.Deserialize<EntraIdServicePrincipalCriteria>(policy.Criteria);
                if (criteria is null)
                {
                    return FederatedCredentialPolicyValidationResult.BadRequest(
                        "Invalid criteria format for Entra ID service principal policy.",
                        nameof(FederatedCredentialPolicy.Criteria));
                }

                if (!IsTenantAllowed(criteria.TenantId))
                {
                    return FederatedCredentialPolicyValidationResult.Unauthorized(
                        $"The Entra ID tenant '{criteria.TenantId}' is not in the allow list.",
                        nameof(FederatedCredentialPolicy.Criteria));
                }
            }

            return base.ValidatePolicy(policy);
        }

        public override async Task<TokenValidationResult> ValidateTokenAsync(JsonWebToken jwt)
        {
            if (string.IsNullOrWhiteSpace(_configuration.EntraIdAudience))
            {
                throw new InvalidOperationException("Unable to validate Entra ID token. Entra ID audience is not configured.");
            }

            var tokenValidationParameters = new TokenValidationParameters
            {
                IssuerValidator = AadIssuerValidator.GetAadIssuerValidator(Issuer).Validate,
                ValidAudience = _configuration.EntraIdAudience,
                ConfigurationManager = _oidcConfigManager,
            };

            tokenValidationParameters.EnableAadSigningKeyIssuerValidation();

            var result = await _jsonWebTokenHandler.ValidateTokenAsync(jwt, tokenValidationParameters);

            return result;
        }

        public override Task<FederatedCredentialPolicyResult> EvaluatePolicyAsync(FederatedCredentialPolicy policy, JsonWebToken jwt)
        {
            string? error;
            switch (policy.Type)
            {
                case FederatedCredentialType.EntraIdServicePrincipal:
                    error = EvaluateEntraIdServicePrincipal(policy, jwt);
                    break;
                case FederatedCredentialType.AzurePipelines:
                    error = EvaluateAzurePipelines(policy, jwt);
                    break;
                default:
                    return Task.FromResult(FederatedCredentialPolicyResult.NotApplicable);
            }

            if (error is not null)
            {
                return Task.FromResult(FederatedCredentialPolicyResult.Unauthorized(error));
            }

            return Task.FromResult(FederatedCredentialPolicyResult.Success);
        }

        /// <summary>
        /// Evaluates an Entra ID service principal federated credential policy against a validated JWT token.
        /// This method validates that the token contains the required claims for an Entra ID service principal
        /// authentication flow and that the claims match the policy criteria.
        /// </summary>
        /// <returns>
        /// <see langword="null"/> if the policy evaluation succeeds; otherwise, an error message describing the validation failure.
        /// </returns>
        private string? EvaluateEntraIdServicePrincipal(FederatedCredentialPolicy policy, JsonWebToken jwt)
        {
            // See https://learn.microsoft.com/en-us/entra/identity-platform/access-token-claims-reference
            const string ClientCredentialTypeClaim = "azpacr";
            const string ClientCertificateType = "2"; // 2 indicates a client certificate (or managed identity) was used
            const string AppIdentityType = "app";
            const string VersionClaim = "ver";
            const string Version2 = "2.0";

            if (!_featureFlagService.CanUseFederatedCredentials(policy.PackageOwner))
            {
                return $"The package owner '{policy.PackageOwner.Username}' is not enabled to use federated credentials.";
            }

            string? error = TryGetRequiredClaim(jwt, ClaimConstants.Tid, out var tid);
            if (error != null)
            {
                return error;
            }

            error = TryGetRequiredClaim(jwt, ClaimConstants.Oid, out var oid);
            if (error != null)
            {
                return error;
            }

            error = TryGetRequiredClaim(jwt, ClientCredentialTypeClaim, out var azpacr);
            if (error != null)
            {
                return error;
            }

            if (azpacr != ClientCertificateType)
            {
                return $"The JSON web token must have an {ClientCredentialTypeClaim} claim with a value of {ClientCertificateType}.";
            }

            error = TryGetRequiredClaim(jwt, IdentityTypeClaim, out var idtyp);
            if (error != null)
            {
                return error;
            }

            if (idtyp != AppIdentityType)
            {
                return $"The JSON web token must have an {IdentityTypeClaim} claim with a value of {AppIdentityType}.";
            }

            error = TryGetRequiredClaim(jwt, VersionClaim, out var ver);
            if (error != null)
            {
                return error;
            }

            if (ver != Version2)
            {
                return $"The JSON web token must have a {VersionClaim} claim with a value of {Version2}.";
            }

            if (jwt.Subject != oid)
            {
                return $"The JSON web token {ClaimConstants.Sub} claim must match the {ClaimConstants.Oid} claim.";
            }

            var criteria = JsonSerializer.Deserialize<EntraIdServicePrincipalCriteria>(policy.Criteria);
            if (criteria is null)
            {
                return "The policy criteria is not a valid JSON object.";
            }

            if (string.IsNullOrWhiteSpace(tid) || !Guid.TryParse(tid, out var parsedTid) || parsedTid != criteria.TenantId)
            {
                return $"The JSON web token must have a {ClaimConstants.Tid} claim that matches the policy.";
            }

            if (!IsTenantAllowed(parsedTid))
            {
                return "The tenant ID in the JSON web token is not in allow list.";
            }

            if (string.IsNullOrWhiteSpace(oid) || !Guid.TryParse(oid, out var parsedOid) || parsedOid != criteria.ObjectId)
            {
                return $"The JSON web token must have a {ClaimConstants.Oid} claim that matches the policy.";
            }

            return null;
        }

        private string? EvaluateAzurePipelines(FederatedCredentialPolicy policy, JsonWebToken jwt)
        {
            if (!_featureFlagService.CanUseFederatedCredentials(policy.PackageOwner))
            {
                return $"The package owner '{policy.PackageOwner.Username}' is not enabled to use federated credentials.";
            }

            if (ValidateClaimExactMatch(
                jwt,
                IdentityTypeClaim,
                FederatedManagedIdentityType,
                StringComparison.Ordinal) is string error)
            {
                return error;
            }

            if (TryGetRequiredClaim(jwt, ClaimConstants.Sub, out var subject) is string subjectError)
            {
                return subjectError;
            }

            if (!FederatedManagedIdentitySubjectRegex.IsMatch(subject))
            {
                return $"The JSON web token {ClaimConstants.Sub} claim must be a structured Federated Managed Identity subject.";
            }

            var criteria = AzurePipelinesCriteria.FromDatabaseJson(policy.Criteria);
            if (criteria.Validate() is string criteriaError)
            {
                return criteriaError;
            }

            if (!jwt.TryGetPayloadValue<JsonElement>(AzurePipelinesAttributesClaim, out var attributes)
                || attributes.ValueKind != JsonValueKind.Object)
            {
                return string.Format(MissingClaimError, AzurePipelinesAttributesClaim);
            }

            if (!attributes.TryGetProperty(AzureDevOpsAttributeNamespace, out var azureDevOpsAttributes)
                || azureDevOpsAttributes.ValueKind != JsonValueKind.Object)
            {
                return $"The JSON Web Token claim '{AzurePipelinesAttributesClaim}' is missing the Azure DevOps attribute namespace.";
            }

            return ValidateAzurePipelinesAttribute(azureDevOpsAttributes, AzurePipelinesCriteria.OrganizationIdClaim, criteria.OrganizationId)
                ?? ValidateAzurePipelinesAttribute(azureDevOpsAttributes, AzurePipelinesCriteria.ProjectIdClaim, criteria.ProjectId)
                ?? ValidateAzurePipelinesAttribute(azureDevOpsAttributes, AzurePipelinesCriteria.DefinitionIdClaim, criteria.DefinitionId)
                ?? ValidateAzurePipelinesAttribute(azureDevOpsAttributes, AzurePipelinesCriteria.RepositoryIdClaim, criteria.RepositoryId)
                ?? ValidateAzurePipelinesAttribute(azureDevOpsAttributes, AzurePipelinesCriteria.RepositoryRefClaim, criteria.RepositoryRef);
        }

        private static string? ValidateAzurePipelinesAttribute(JsonElement attributes, string claim, string expectedValue)
        {
            if (!attributes.TryGetProperty(claim, out var value)
                || value.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(value.GetString()))
            {
                return string.Format(MissingClaimError, $"{AzurePipelinesAttributesClaim}.{claim}");
            }

            var actualValue = value.GetString()!;
            if (!actualValue.Equals(expectedValue, StringComparison.Ordinal))
            {
                return string.Format(ClaimMismatchError, $"{AzurePipelinesAttributesClaim}.{claim}", actualValue);
            }

            return null;
        }

        private static string GetPolicyTypeDisplayName(FederatedCredentialType type)
        {
            return type switch
            {
                FederatedCredentialType.EntraIdServicePrincipal => "Entra ID service principal",
                FederatedCredentialType.AzurePipelines => "Azure Pipelines",
                _ => type.ToString(),
            };
        }

        private bool IsTenantAllowed(Guid tenantId)
        {
            if (_configuration.AllowedEntraIdTenants.Length == 0)
            {
                return false;
            }

            if (_configuration.AllowedEntraIdTenants.Length == 1
                && _configuration.AllowedEntraIdTenants[0] == "all")
            {
                return true;
            }

            var tenantIdString = tenantId.ToString();
            return _configuration.AllowedEntraIdTenants.Contains(tenantIdString, StringComparer.OrdinalIgnoreCase);
        }
    }
}
