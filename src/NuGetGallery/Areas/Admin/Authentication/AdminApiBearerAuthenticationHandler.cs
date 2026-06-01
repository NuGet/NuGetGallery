// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Logging;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Microsoft.IdentityModel.Validators;
using Microsoft.Owin;
using Microsoft.Owin.Security;
using Microsoft.Owin.Security.Infrastructure;
using NuGetGallery.Configuration;
using NuGetGallery.Services.Authentication;

namespace NuGetGallery.Areas.Admin.Authentication
{
    /// <summary>
    /// OWIN authentication handler that validates Entra ID bearer tokens for Admin API
    /// requests. Uses the OWIN authentication framework so it integrates with IIS pipeline
    /// stages via <see cref="Microsoft.Owin.Extensions.IntegratedPipelineExtensions.UseStageMarker"/>.
    /// </summary>
    public class AdminApiBearerAuthenticationHandler
        : AuthenticationHandler<AdminApiBearerAuthenticationOptions>
    {
        internal const string TidClaim = "tid";
        internal const string AzpClaim = "azp";
        internal const string CallerIdentityClaim = "AdminApi.CallerIdentity";

        protected override async Task<AuthenticationTicket> AuthenticateCoreAsync()
        {
            var token = ExtractBearerToken();
            if (string.IsNullOrEmpty(token))
            {
                StoreAuthError(HttpStatusCode.Unauthorized, "A valid Bearer token must be provided in the Authorization header.");
                return null;
            }

            var configService = DependencyResolver.Current.GetService<IGalleryConfigurationService>();
            var result = await ValidateAndAuthorizeAsync(token, configService);

            if (result.StatusCode != 0)
            {
                StoreAuthError((HttpStatusCode)result.StatusCode, result.Message);
                return null;
            }

            // Run additional validators (e.g. MISE) loaded from the add-ins directory.
            // This mirrors the pattern used by FederatedCredentialPolicyEvaluator for
            // trusted publishing. Both MSAL and MISE validation must pass.
            var additionalValidators = DependencyResolver.Current.GetService<IReadOnlyList<IFederatedCredentialValidator>>();
            if (additionalValidators != null)
            {
                var requestHeaders = ToNameValueCollection(Request.Headers);
                foreach (var validator in additionalValidators)
                {
                    var validationResult = await validator.ValidateAsync(
                        requestHeaders,
                        FederatedCredentialIssuerType.EntraId,
                        unvalidatedClaims: null);

                    if (validationResult.Type == FederatedCredentialValidationType.Unauthorized)
                    {
                        StoreAuthError(HttpStatusCode.Unauthorized, "Additional token validation failed.");
                        return null;
                    }
                }
            }

            var callerIdentity = $"Tenant ID: {result.TenantId}, Client ID: {result.AuthorizedParty}";

            var identity = new ClaimsIdentity(Options.AuthenticationType, ClaimTypes.Name, ClaimTypes.Role);
            identity.AddClaim(new Claim(TidClaim, result.TenantId));
            identity.AddClaim(new Claim(AzpClaim, result.AuthorizedParty));
            identity.AddClaim(new Claim(CallerIdentityClaim, callerIdentity));

            return new AuthenticationTicket(identity, new AuthenticationProperties());
        }

        protected override Task ApplyResponseChallengeAsync()
        {
            if (Response.StatusCode == (int)HttpStatusCode.Unauthorized)
            {
                var challenge = Helper.LookupChallenge(Options.AuthenticationType, Options.AuthenticationMode);
                if (challenge != null)
                {
                    Response.Headers.Append("WWW-Authenticate", $"Bearer realm=\"{Request.Uri.Host}\"");
                }
            }

            return Task.CompletedTask;
        }

        private void StoreAuthError(HttpStatusCode statusCode, string message)
        {
            Context.Environment[AdminApiBearerAuthenticationOptions.AuthErrorEnvironmentKey] = new AuthError { StatusCode = statusCode, Message = message };
        }

        private string ExtractBearerToken()
        {
            var authHeader = Request.Headers["Authorization"];
            if (string.IsNullOrEmpty(authHeader))
            {
                return null;
            }

            const string bearerPrefix = "Bearer ";
            if (authHeader.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return authHeader[bearerPrefix.Length..].Trim();
            }

            return null;
        }

        private static NameValueCollection ToNameValueCollection(IHeaderDictionary headers)
        {
            var nvc = new NameValueCollection();
            foreach (var header in headers)
            {
                foreach (var value in header.Value)
                {
                    nvc.Add(header.Key, value);
                }
            }

            return nvc;
        }

        internal static async Task<AuthResult> ValidateAndAuthorizeAsync(
            string token,
            IGalleryConfigurationService configService)
        {
            var config = configService.Current;

            var handler = new JsonWebTokenHandler();
            var oidcConfigManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                EntraIdTokenPolicyValidator.MetadataAddress,
                new OpenIdConnectConfigurationRetriever());

            var tokenValidationParameters = new TokenValidationParameters
            {
                ConfigurationManager = oidcConfigManager,
            };

            var isTestMode = config.AdminApiTestModeEnabled;
            if (isTestMode)
            {
                // Test mode: skip all token validation so that self-signed
                // test JWTs can exercise the claims and allowed-callers checks.
                tokenValidationParameters.ValidateIssuer = false;
                tokenValidationParameters.ValidateAudience = false;
                tokenValidationParameters.ValidateLifetime = false;
                tokenValidationParameters.ValidateIssuerSigningKey = false;
                tokenValidationParameters.SignatureValidator = (token, parameters) => new JsonWebToken(token);

                IdentityModelEventSource.ShowPII = true;
                IdentityModelEventSource.LogCompleteSecurityArtifact = true;
            }
            else
            {
                tokenValidationParameters.IssuerValidator = AadIssuerValidator.GetAadIssuerValidator(EntraIdTokenPolicyValidator.Issuer).Validate;
                tokenValidationParameters.ValidAudience = config.AdminApiAudience;
                tokenValidationParameters.EnableAadSigningKeyIssuerValidation();
            }

            TokenValidationResult validationResult;
            try
            {
                validationResult = await handler.ValidateTokenAsync(token, tokenValidationParameters).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return new AuthResult
                {
                    StatusCode = (int)HttpStatusCode.Unauthorized,
                    Message = $"Token validation failed: {ex.Message}"
                };
            }

            if (!validationResult.IsValid)
            {
                var reason = validationResult.Exception?.Message ?? "Unknown validation error";
                return new AuthResult
                {
                    StatusCode = (int)HttpStatusCode.Unauthorized,
                    Message = $"Token is invalid: {reason}"
                };
            }

            if (validationResult.SecurityToken is not JsonWebToken jwt)
            {
                return new AuthResult
                {
                    StatusCode = (int)HttpStatusCode.Unauthorized,
                    Message = "Token is not a valid JSON Web Token."
                };
            }

            string tid;
            string azp;
            try
            {
                tid = jwt.GetClaim(TidClaim)?.Value;
                azp = jwt.GetClaim(AzpClaim)?.Value;
            }
            catch
            {
                return new AuthResult
                {
                    StatusCode = (int)HttpStatusCode.Forbidden,
                    Message = "Failed to read required claims from the token."
                };
            }

            if (string.IsNullOrWhiteSpace(tid) || string.IsNullOrWhiteSpace(azp))
            {
                return new AuthResult
                {
                    StatusCode = (int)HttpStatusCode.Forbidden,
                    Message = $"Token is missing required claims. tid={!string.IsNullOrWhiteSpace(tid)}, azp={!string.IsNullOrWhiteSpace(azp)}."
                };
            }

            var allowedCallers = ParseAllowedCallers(config.AdminApiAllowedCallers);
            if (!allowedCallers.Any(c =>
                string.Equals(c.TenantId, tid, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(c.AuthorizedParty, azp, StringComparison.OrdinalIgnoreCase)))
            {
                return new AuthResult
                {
                    StatusCode = (int)HttpStatusCode.Forbidden,
                    Message = $"Caller tid={tid} azp={azp} is not in the allowed callers list."
                };
            }

            return new AuthResult { StatusCode = 0, AuthorizedParty = azp, TenantId = tid };
        }

        internal static List<AllowedCaller> ParseAllowedCallers(string configValue)
        {
            var result = new List<AllowedCaller>();
            if (string.IsNullOrWhiteSpace(configValue))
            {
                return result;
            }

            var pairs = configValue.Split([';'], StringSplitOptions.RemoveEmptyEntries);
            foreach (var pair in pairs)
            {
                var parts = pair.Split([':'], 2);
                if (parts.Length == 2 &&
                    !string.IsNullOrWhiteSpace(parts[0]) &&
                    !string.IsNullOrWhiteSpace(parts[1]))
                {
                    result.Add(new AllowedCaller
                    {
                        TenantId = parts[0].Trim(),
                        AuthorizedParty = parts[1].Trim()
                    });
                }
            }

            return result;
        }

        internal class AuthResult
        {
            public int StatusCode { get; set; }
            public string Message { get; set; }
            public string AuthorizedParty { get; set; }
            public string TenantId { get; set; }
        }

        internal class AllowedCaller
        {
            public string TenantId { get; set; }
            public string AuthorizedParty { get; set; }
        }

        internal class AuthError
        {
            public HttpStatusCode StatusCode { get; set; }
            public string Message { get; set; }
        }
    }
}
