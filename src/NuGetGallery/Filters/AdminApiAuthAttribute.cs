// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Microsoft.IdentityModel.Validators;
using NuGetGallery.Configuration;
using NuGetGallery.Services.Authentication;

namespace NuGetGallery.Filters
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public class AdminApiAuthAttribute : FilterAttribute, IAuthorizationFilter
    {
        internal static readonly string AzpItemKey = "AdminApi.AuthorizedParty";

        internal const string TidClaim = "tid";
        internal const string AzpClaim = "azp";

        public void OnAuthorization(AuthorizationContext filterContext)
        {
            var configService = DependencyResolver.Current.GetService<IGalleryConfigurationService>();

            if (!configService.Current.AdminPanelEnabled || !configService.Current.AdminApiEnabled)
            {
                filterContext.Result = new HttpStatusCodeResult(HttpStatusCode.NotFound);
                return;
            }

            var token = ExtractBearerToken(filterContext.HttpContext.Request);
            if (string.IsNullOrEmpty(token))
            {
                filterContext.Result = new HttpStatusCodeResult(HttpStatusCode.Unauthorized);
                return;
            }

            var validationTask = ValidateAndAuthorizeAsync(token, configService);
            var result = AsyncHelper.RunSync(() => validationTask);

            if (result.StatusCode != 0)
            {
                filterContext.Result = new HttpStatusCodeResult(result.StatusCode);
                return;
            }

            filterContext.HttpContext.Items[AzpItemKey] = result.AuthorizedParty;
        }

        internal static string ExtractBearerToken(HttpRequestBase request)
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
                handler = DependencyResolver.Current.GetService<JsonWebTokenHandler>();
                oidcConfigManager = DependencyResolver.Current
                    .GetService<ConfigurationManager<OpenIdConnectConfiguration>>();
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

            TokenValidationResult validationResult;
            try
            {
                validationResult = await handler.ValidateTokenAsync(token, tokenValidationParameters).ConfigureAwait(false);
            }
            catch
            {
                return new AuthResult { StatusCode = (int)HttpStatusCode.Unauthorized };
            }

            if (!validationResult.IsValid)
            {
                return new AuthResult { StatusCode = (int)HttpStatusCode.Unauthorized };
            }

            if (validationResult.SecurityToken is not JsonWebToken jwt)
            {
                return new AuthResult { StatusCode = (int)HttpStatusCode.Unauthorized };
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
                return new AuthResult { StatusCode = (int)HttpStatusCode.Forbidden };
            }

            if (string.IsNullOrEmpty(tid) || string.IsNullOrEmpty(azp))
            {
                return new AuthResult { StatusCode = (int)HttpStatusCode.Forbidden };
            }

            var allowedCallers = ParseAllowedCallers(config.AdminApiAllowedCallers);
            if (!allowedCallers.Any(c =>
                string.Equals(c.TenantId, tid, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(c.AuthorizedParty, azp, StringComparison.OrdinalIgnoreCase)))
            {
                return new AuthResult { StatusCode = (int)HttpStatusCode.Forbidden };
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

        internal class AuthResult
        {
            public int StatusCode { get; set; }
            public string AuthorizedParty { get; set; }
        }

        internal class AllowedCaller
        {
            public string TenantId { get; set; }
            public string AuthorizedParty { get; set; }
        }
    }

    internal static class AsyncHelper
    {
        private static readonly TaskFactory TaskFactory = new(
            CancellationToken.None,
            TaskCreationOptions.None,
            TaskContinuationOptions.None,
            TaskScheduler.Default);

        public static TResult RunSync<TResult> (Func<Task<TResult>> func) =>
            TaskFactory
                .StartNew(func)
                .Unwrap()
                .GetAwaiter()
                .GetResult();
    }
}
