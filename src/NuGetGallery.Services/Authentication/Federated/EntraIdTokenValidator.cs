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
            if (policy.Type != FederatedCredentialType.EntraIdServicePrincipal)
            {
                // We do not expect callers to pass non-Entra ID policies to this validator.
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
                    "Criteria must be provided for Entra ID service principal policies.",
                    nameof(FederatedCredentialPolicy.Criteria));
            }

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
            if (policy.Type != FederatedCredentialType.EntraIdServicePrincipal)
            {
                return Task.FromResult(FederatedCredentialPolicyResult.NotApplicable);
            }

            if (EvaluateEntraIdServicePrincipal(policy, jwt) is string error)
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
