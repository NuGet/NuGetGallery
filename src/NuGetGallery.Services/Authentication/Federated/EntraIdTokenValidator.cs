// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Text.Json;
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
            if (policy.Type != FederatedCredentialType.EntraIdServicePrincipal && policy.Type != FederatedCredentialType.AzureDevOps)
            {
                // We do not expect callers to pass non-Entra ID and non-Azure DevOps policies to this validator.
                return FederatedCredentialPolicyValidationResult.BadRequest(
                    $"Invalid policy type '{policy.Type}' for Entra ID validation.",
                    policyPropertyName: null);
            }

            if (string.IsNullOrWhiteSpace(policy.Criteria))
            {
                return FederatedCredentialPolicyValidationResult.BadRequest(
                    $"Criteria must be provided for {policy.Type} policies.",
                    nameof(FederatedCredentialPolicy.Criteria));
            }

            if (policy.Type == FederatedCredentialType.EntraIdServicePrincipal)
            {
                if (!_featureFlagService.CanUseFederatedCredentials(policy.PackageOwner))
                {
                    return FederatedCredentialPolicyValidationResult.BadRequest(
                        $"The package owner '{policy.PackageOwner.Username}' is not enabled to use federated credentials.",
                        nameof(FederatedCredentialPolicy.PackageOwner));
                }

                EntraIdServicePrincipalCriteria? entraIdServicePrincipalCriteria;
                try
                {
                    entraIdServicePrincipalCriteria = JsonSerializer.Deserialize<EntraIdServicePrincipalCriteria>(policy.Criteria);
                }
                catch (JsonException)
                {
                    return FederatedCredentialPolicyValidationResult.BadRequest(
                        "The policy criteria is not in a valid JSON format.",
                        nameof(FederatedCredentialPolicy.Criteria));
                }

                if (entraIdServicePrincipalCriteria is null)
                {
                    return FederatedCredentialPolicyValidationResult.BadRequest(
                        "The policy criteria is not in a valid JSON format.",
                        nameof(FederatedCredentialPolicy.Criteria));
                }

                if (!IsTenantAllowed(entraIdServicePrincipalCriteria.TenantId))
                {
                    return FederatedCredentialPolicyValidationResult.Unauthorized(
                        $"The Entra ID tenant '{entraIdServicePrincipalCriteria.TenantId}' is not in the allow list.",
                        nameof(FederatedCredentialPolicy.Criteria));
                }
            }
            else
            {
                if (!_featureFlagService.CanUseAzureDevOpsCredentials(policy.PackageOwner))
                {
                    return FederatedCredentialPolicyValidationResult.BadRequest(
                        $"The package owner '{policy.PackageOwner.Username}' is not enabled to use Azure DevOps credentials.",
                        nameof(FederatedCredentialPolicy.PackageOwner));
                }

                AzureDevOpsCriteria? azureDevOpsCriteria;
                try
                {
                    azureDevOpsCriteria = JsonSerializer.Deserialize<AzureDevOpsCriteria>(policy.Criteria);
                }
                catch (JsonException)
                {
                    return FederatedCredentialPolicyValidationResult.BadRequest(
                        "The policy criteria is not in a valid JSON format.",
                        nameof(FederatedCredentialPolicy.Criteria));
                }

                if (azureDevOpsCriteria is null)
                {
                    return FederatedCredentialPolicyValidationResult.BadRequest(
                        "The policy criteria is not in a valid JSON format.",
                        nameof(FederatedCredentialPolicy.Criteria));
                }

                var errorForCriteria = "";
                if (!azureDevOpsCriteria.IsValid(out errorForCriteria))
                {
                    return FederatedCredentialPolicyValidationResult.BadRequest(
                        "The policy criteria is not valid" + (string.IsNullOrEmpty(errorForCriteria) ? "." : $": {errorForCriteria}"),
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
            if (policy.Type != FederatedCredentialType.EntraIdServicePrincipal && policy.Type != FederatedCredentialType.AzureDevOps)
            {
                return Task.FromResult(FederatedCredentialPolicyResult.NotApplicable);
            }

            switch (policy.Type)
            {
                case FederatedCredentialType.EntraIdServicePrincipal:
                    if (EvaluateEntraIdServicePrincipalPolicy(policy, jwt) is string errorForEntraIdServicePrincipalPolicy)
                    {
                        return Task.FromResult(FederatedCredentialPolicyResult.Unauthorized(errorForEntraIdServicePrincipalPolicy));
                    }

                    break;
                case FederatedCredentialType.AzureDevOps:
                    if (EvaluateAzureDevOpsPolicy(policy, jwt) is string errorForAzureDevOpsPolicy)
                    {
                        return Task.FromResult(FederatedCredentialPolicyResult.Unauthorized(errorForAzureDevOpsPolicy));
                    }

                    break;
                default:
                    return Task.FromResult(FederatedCredentialPolicyResult.NotApplicable);
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
        private string? EvaluateEntraIdServicePrincipalPolicy(FederatedCredentialPolicy policy, JsonWebToken jwt)
        {
            // See https://learn.microsoft.com/en-us/entra/identity-platform/access-token-claims-reference
            const string ClientCredentialTypeClaim = "azpacr";
            const string ClientCertificateType = "2"; // 2 indicates a client certificate (or managed identity) was used
            const string IdentityTypeClaim = "idtyp";
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

            EntraIdServicePrincipalCriteria? criteria;
            try
            {
                criteria = JsonSerializer.Deserialize<EntraIdServicePrincipalCriteria>(policy.Criteria);
            }
            catch (JsonException)
            {
                return "The policy criteria is not in a valid JSON format.";
            }

            if (criteria is null)
            {
                return "The policy criteria is not in a valid JSON format.";
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

        /// <summary>
        /// Evaluates an Azure DevOps policy against a validated JWT token.
        /// This method validates that the token contains the required claims and that the claims match the policy criteria.
        /// </summary>
        /// <returns>
        /// <see langword="null"/> if the policy evaluation succeeds; otherwise, an error message describing the validation failure.
        /// </returns>
        private string? EvaluateAzureDevOpsPolicy(FederatedCredentialPolicy policy, JsonWebToken jwt)
        {
            const string IdentityTypeClaim = "idtyp";
            const string AppIdentityType = "fmi";
            const string AttributeClaim = "xms_attr";
            const string AttributeNamespace = "rISbSSETf0KqFyZ8ppdXmA";
            const string OrganizationIdAttribute = "org_id";
            const string PipelineIdAttribute = "def_id";

            if (!_featureFlagService.CanUseAzureDevOpsCredentials(policy.PackageOwner))
            {
                return $"The package owner '{policy.PackageOwner.Username}' is not enabled to use Azure DevOps credentials.";
            }

            string? error = ValidateClaimExactMatch(jwt, claim: IdentityTypeClaim, expectedValue: AppIdentityType, StringComparison.Ordinal);
            if (error != null)
            {
                return error;
            }

            error = TryGetRequiredClaim<JsonElement>(jwt, AttributeClaim, out var xms_attr);
            if (error != null)
            {
                return error;
            }

            if (!xms_attr.TryGetProperty(AttributeNamespace, out var attributes) || attributes.ValueKind != JsonValueKind.Object)
            {
                return $"The {AttributeClaim} claim in the JSON web token does not contain the valid namespace.";
            }

            if (!attributes.TryGetProperty(OrganizationIdAttribute, out var organizationId) ||
                organizationId.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(organizationId.GetString()))
            {
                return $"The {AttributeClaim} claim in the JSON web token does not contain the valid organization Id.";
            }

            if (!attributes.TryGetProperty(PipelineIdAttribute, out var pipelineId) ||
                pipelineId.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(pipelineId.GetString()))
            {
                return $"The {AttributeClaim} claim in the JSON web token does not contain the valid pipeline Id.";
            }

            AzureDevOpsCriteria? criteria;
            try
            {
                criteria = JsonSerializer.Deserialize<AzureDevOpsCriteria>(policy.Criteria);
            }
            catch (JsonException)
            {
                return "The policy criteria is not in a valid JSON format.";
            }

            if (criteria is null)
            {
                return "The policy criteria is not in a valid JSON format.";
            }

            var errorForCriteria = "";
            if (!criteria.IsValid(out errorForCriteria))
            {
                return "The policy criteria is not valid" + (string.IsNullOrEmpty(errorForCriteria) ? "." : $": {errorForCriteria}");
            }

            if (!criteria.OrganizationId.Equals(organizationId.ToString(), StringComparison.Ordinal))
            {
                return "The organization Id in the JSON web token does not match the policy.";
            }

            if (!criteria.PipelineId.Equals(pipelineId.ToString(), StringComparison.Ordinal))
            {
                return "The pipeline Id in the JSON web token does not match the policy.";
            }

            return null;
        }
    }
}
