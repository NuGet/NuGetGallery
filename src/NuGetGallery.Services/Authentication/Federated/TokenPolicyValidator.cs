// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Cryptography;
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
    /// Provides an abstract base class for validating token policies in federated authentication scenarios.
    /// </summary>
    public abstract class TokenPolicyValidator : ITokenPolicyValidator
    {
        public const string MissingClaimError = "The JSON Web Token is missing the required claim '{0}'.";
        public const string ClaimMismatchError = "The JSON Web Token claim '{0}' has value '{1}' which does not match the policy.";

        protected readonly ConfigurationManager<OpenIdConnectConfiguration> _oidcConfigManager;
        protected readonly JsonWebTokenHandler _jsonWebTokenHandler;
        protected readonly IFederatedCredentialConfiguration _configuration;
        private readonly string _tokenIdentifierClaimName;

        protected TokenPolicyValidator(
            ConfigurationManager<OpenIdConnectConfiguration> oidcConfigManager,
            IFederatedCredentialConfiguration configuration,
            JsonWebTokenHandler jsonWebTokenHandler,
            string tokenIdentifierClaimName = "jti")
        {
            _oidcConfigManager = oidcConfigManager ?? throw new ArgumentNullException(nameof(oidcConfigManager));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _jsonWebTokenHandler = jsonWebTokenHandler ?? throw new ArgumentNullException(nameof(jsonWebTokenHandler));
            _tokenIdentifierClaimName = tokenIdentifierClaimName ?? throw new ArgumentNullException(nameof(tokenIdentifierClaimName));
        }

        public abstract string IssuerAuthority { get; }
        public abstract FederatedCredentialIssuerType IssuerType { get; }
        public abstract Task<TokenValidationResult> ValidateTokenAsync(JsonWebToken token);
        public abstract Task<FederatedCredentialPolicyResult> EvaluatePolicyAsync(FederatedCredentialPolicy policy, JsonWebToken jwt);

        public (string? tokenId, string? error) ValidateTokenIdentifier(JsonWebToken jwt)
        {
            if (!jwt.TryGetPayloadValue<string>(_tokenIdentifierClaimName, out var tokenId)
                || string.IsNullOrWhiteSpace(tokenId))
            {
                return (null, $"The JSON web token must have a {_tokenIdentifierClaimName} claim.");
            }

            return (tokenId, null);
        }


        /// <summary>
        /// Attempts to get a required string claim from the JWT token.
        /// </summary>
        /// <returns><see langword="null"/> if the claim value present; otherwise, an error message.</returns>
        protected static string? TryGetRequiredClaim(JsonWebToken jwt, string claim, out string claimValue)
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
        protected static string? ValidateClaimExactMatch(JsonWebToken jwt, string claim, string expectedValue, StringComparison comparison)
        {
            if (TryGetRequiredClaim(jwt, claim, out string claimValue) is string error)
            {
                return error;
            }

            if (!claimValue.Equals(expectedValue, comparison))
            {
                return string.Format(ClaimMismatchError, claim, claimValue);
            }

            return null;
        }

        /// <summary>
        /// Validates that a JWT claim exists and starts with the expected value using the specified string comparison.
        /// </summary>
        /// <returns><see langword="null"/> if the claim is valid; otherwise, an error message.</returns>
        protected static string? ValidateClaimStartsWith(JsonWebToken jwt, string claim, string expectedValue, StringComparison comparison)
        {
            if (TryGetRequiredClaim(jwt, claim, out string claimValue) is string error)
            {
                return error;
            }

            if (!claimValue.StartsWith(expectedValue, comparison))
            {
                return string.Format(ClaimMismatchError, claim, claimValue);
            }

            return null;
        }

        public virtual FederatedCredentialPolicyValidationResult ValidatePolicy(FederatedCredentialPolicy policy)
        {
            if (policy.CreatedBy is Organization)
            {
                return FederatedCredentialPolicyValidationResult.BadRequest(
                    $"Policy user '{policy.CreatedBy.Username}' is an organization. Creating federated credential trust policies for organizations is not supported.",
                    nameof(FederatedCredentialPolicy.CreatedBy));
            }

            // If policy name is provided then it should not be too long.
            if (policy.PolicyName?.Length > FederatedCredentialPolicy.MaxPolicyNameLength)
            {
                return FederatedCredentialPolicyValidationResult.Unauthorized(
                    $"The policy name cannot be longer than {FederatedCredentialPolicy.MaxPolicyNameLength}.",
                    nameof(FederatedCredentialPolicy.PolicyName));
            }

            return FederatedCredentialPolicyValidationResult.Success(policy);
        }
    }
}
