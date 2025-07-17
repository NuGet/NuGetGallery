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
    /// Provides an abstract base class for validating token policies in federated authentication scenarios.
    /// </summary>
    public abstract class TokenPolicyValidator : ITokenPolicyValidator
    {
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
    }
}
