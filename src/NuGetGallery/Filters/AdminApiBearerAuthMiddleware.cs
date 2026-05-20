// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Logging;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Microsoft.IdentityModel.Validators;
using Microsoft.Owin;
using NuGetGallery.Configuration;
using NuGetGallery.Services.Authentication;

namespace NuGetGallery.Filters
{
    /// <summary>
    /// OWIN middleware that validates Entra ID bearer tokens for Admin API requests.
    /// This runs natively async in the OWIN pipeline, avoiding the sync-over-async
    /// pattern that would be required in an MVC <see cref="IAuthorizationFilter"/>.
    /// </summary>
    public class AdminApiBearerAuthMiddleware : OwinMiddleware
    {
        internal const string PathPrefix = "/api/admin/";

        internal const string TidClaim = "tid";
        internal const string AzpClaim = "azp";

        public AdminApiBearerAuthMiddleware(OwinMiddleware next) : base(next)
        {
        }

        public override async Task Invoke(IOwinContext context)
        {
            if (!context.Request.Path.HasValue ||
                !context.Request.Path.Value.StartsWith(PathPrefix, StringComparison.OrdinalIgnoreCase))
            {
                await Next.Invoke(context);
                return;
            }

            var token = ExtractBearerToken(context.Request);
            if (string.IsNullOrEmpty(token))
            {
                await WriteErrorResponseAsync(context, HttpStatusCode.Unauthorized,
                    "A valid Bearer token must be provided in the Authorization header.");
                return;
            }

            var configService = DependencyResolver.Current.GetService<IGalleryConfigurationService>();
            var result = await ValidateAndAuthorizeAsync(token, configService);

            if (result.StatusCode != 0)
            {
                await WriteErrorResponseAsync(context, (HttpStatusCode)result.StatusCode, result.Message);
                return;
            }

            context.Environment[AdminApiAuthAttribute.AzpItemKey] = result.AuthorizedParty;

            await Next.Invoke(context);
        }

        internal static string ExtractBearerToken(IOwinRequest request)
        {
            var authHeader = request.Headers["Authorization"];
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

        internal static async Task<AuthResult> ValidateAndAuthorizeAsync(
            string token,
            IGalleryConfigurationService configService)
        {
            var config = configService.Current;

            JsonWebTokenHandler handler;
            ConfigurationManager<OpenIdConnectConfiguration> oidcConfigManager;

            try
            {
                handler = DependencyResolver.Current.GetService<JsonWebTokenHandler>() ??
                    new JsonWebTokenHandler();
                oidcConfigManager = DependencyResolver.Current.GetService<ConfigurationManager<OpenIdConnectConfiguration>>() ??
                    new ConfigurationManager<OpenIdConnectConfiguration>(
                        EntraIdTokenPolicyValidator.MetadataAddress,
                        new OpenIdConnectConfigurationRetriever());
            }
            catch
            {
                handler = new JsonWebTokenHandler();
                oidcConfigManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                    EntraIdTokenPolicyValidator.MetadataAddress,
                    new OpenIdConnectConfigurationRetriever());
            }

            var tokenValidationParameters = new TokenValidationParameters
            {
                IssuerValidator = AadIssuerValidator
                    .GetAadIssuerValidator(EntraIdTokenPolicyValidator.Issuer).Validate,
                ValidAudience = config.AdminApiAudience,
                ConfigurationManager = oidcConfigManager,
            };

            tokenValidationParameters.EnableAadSigningKeyIssuerValidation();

#if DEBUG
            IdentityModelEventSource.ShowPII = true;
            IdentityModelEventSource.LogCompleteSecurityArtifact = true;
#endif

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

            if (string.IsNullOrEmpty(tid) || string.IsNullOrEmpty(azp))
            {
                return new AuthResult
                {
                    StatusCode = (int)HttpStatusCode.Forbidden,
                    Message = $"Token is missing required claims. tid={!string.IsNullOrEmpty(tid)}, azp={!string.IsNullOrEmpty(azp)}."
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

            return new AuthResult { StatusCode = 0, AuthorizedParty = azp };
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

        private static async Task WriteErrorResponseAsync(IOwinContext context, HttpStatusCode statusCode, string message)
        {
            context.Response.StatusCode = (int)statusCode;
            context.Response.ContentType = "text/plain";
            await context.Response.WriteAsync(message);
        }

        internal class AuthResult
        {
            public int StatusCode { get; set; }
            public string Message { get; set; }
            public string AuthorizedParty { get; set; }
        }

        internal class AllowedCaller
        {
            public string TenantId { get; set; }
            public string AuthorizedParty { get; set; }
        }
    }
}
