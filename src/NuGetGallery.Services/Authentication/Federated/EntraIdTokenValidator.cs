// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Tokens;
using Microsoft.IdentityModel.Validators;
using System.Linq;

#nullable enable

namespace NuGetGallery.Services.Authentication
{
    /// <summary>
    /// This interface is used to ensure a given token is issued by Entra ID.
    /// </summary>
    public interface IEntraIdTokenValidator
    {
        /// <summary>
        /// Determines if a given Entra tenant ID GUID is in the allow list.
        /// </summary>
        bool IsTenantAllowed(Guid tenantId);

        /// <summary>
        /// Perform minimal validation of the token to ensure it was issued by Entra ID. Validations:
        /// - Expected issuer (Entra ID)
        /// - Expected audience
        /// - Valid signature
        /// - Not expired
        /// </summary>
        /// <param name="token">The parsed JWT</param>
        /// <returns>The token validation result, check the <see cref="TokenValidationResult.IsValid"/> for success or failure.</returns>
        Task<TokenValidationResult> ValidateAsync(JsonWebToken token);
    }

    public class EntraIdTokenValidator : IEntraIdTokenValidator
    {
        private static string EntraIdAuthority { get; } = "https://login.microsoftonline.com/common/v2.0";
        public static string MetadataAddress { get; } = $"{EntraIdAuthority}/.well-known/openid-configuration";

        private readonly ConfigurationManager<OpenIdConnectConfiguration> _oidcConfigManager;
        private readonly JsonWebTokenHandler _jsonWebTokenHandler;
        private readonly IFederatedCredentialConfiguration _configuration;

        public EntraIdTokenValidator(
            ConfigurationManager<OpenIdConnectConfiguration> oidcConfigManager,
            JsonWebTokenHandler jsonWebTokenHandler,
            IFederatedCredentialConfiguration configuration)
        {
            _oidcConfigManager = oidcConfigManager ?? throw new ArgumentNullException(nameof(oidcConfigManager));
            _jsonWebTokenHandler = jsonWebTokenHandler ?? throw new ArgumentNullException(nameof(jsonWebTokenHandler));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public bool IsTenantAllowed(Guid tenantId)
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

        public async Task<TokenValidationResult> ValidateAsync(JsonWebToken token)
        {
            if (string.IsNullOrWhiteSpace(_configuration.EntraIdAudience))
            {
                throw new InvalidOperationException("Unable to validate Entra ID token. Entra ID audience is not configured.");
            }

            var tokenValidationParameters = new TokenValidationParameters
            {
                IssuerValidator = AadIssuerValidator.GetAadIssuerValidator(EntraIdAuthority).Validate,
                ValidAudience = _configuration.EntraIdAudience,
                ConfigurationManager = _oidcConfigManager,
            };

            tokenValidationParameters.EnableAadSigningKeyIssuerValidation();

            var result = await _jsonWebTokenHandler.ValidateTokenAsync(token, tokenValidationParameters);

            return result;
        }
    }
}
