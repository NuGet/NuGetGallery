// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Owin;
using Microsoft.Owin.Logging;
using Microsoft.Owin.Security;
using Microsoft.Owin.Security.Infrastructure;
using Newtonsoft.Json;
using NuGetGallery.Infrastructure.Authentication;

namespace NuGetGallery.Authentication.Providers.ApiKey
{
    public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
    {
        private ApiKeyAuthenticationOptions _options;

        protected ILogger Logger { get; set; }
        protected AuthenticationService Auth { get; set; }
        protected ICredentialBuilder CredentialBuilder { get; set; }

        private ApiKeyAuthenticationOptions TheOptions { get { return _options ?? Options; } }

        internal ApiKeyAuthenticationHandler() { }

        public ApiKeyAuthenticationHandler(ILogger logger, AuthenticationService auth, ICredentialBuilder credentialBuilder)
        {
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            Auth = auth ?? throw new ArgumentNullException(nameof(auth));
            CredentialBuilder = credentialBuilder ?? throw new ArgumentNullException(nameof(credentialBuilder));
        }

        internal Task InitializeAsync(ApiKeyAuthenticationOptions options, IOwinContext context)
        {
            _options = options; // Override the Options property
            return BaseInitializeAsync(options, context);
        }

        protected override async Task ApplyResponseChallengeAsync()
        {
            if (Response.StatusCode == 401 && (Helper.LookupChallenge(TheOptions.AuthenticationType, TheOptions.AuthenticationMode) != null))
            {
                var apiKey = Request.Headers[TheOptions.ApiKeyHeaderName];
                var authorization = Request.Headers["Authorization"];
                if (!String.IsNullOrEmpty(apiKey) || !String.IsNullOrEmpty(authorization))
                {
                    // Had an API key, but it didn't match a user
                    var statusCode = Context.Get<int>("StatusCode");
                    var message = Context.Get<string>("Message") ?? ServicesStrings.ApiKeyNotAuthorized;
                    WriteStatus(message, statusCode == default ? 401 : statusCode);
                }
                else
                {
                    // Keep the 401, but add the authentication information
                    WriteStatus(ServicesStrings.ApiKeyRequired, 401);
                    Response.Headers.Append("WWW-Authenticate", String.Format(
                        CultureInfo.InvariantCulture,
                        "ApiKey realm=\"{0}\"",
                        Request.Uri.Host));
                }
            }
            else
            {
                await base.ApplyResponseChallengeAsync();
            }
        }

        internal void WriteStatus(string message, int statusCode)
        {
            Response.StatusCode = statusCode;
            Response.ReasonPhrase = message;
            Response.Write(message);
        }

        protected override async Task<AuthenticationTicket> AuthenticateCoreAsync()
        {
            var apiKey = Request.Headers[TheOptions.ApiKeyHeaderName];
            if (!string.IsNullOrEmpty(apiKey))
            {
                return await AuthenticateWithApiKeyHeader(apiKey);
            }

            var authorization = Request.Headers["Authorization"];
            if (!string.IsNullOrEmpty(authorization))
            {
                return await AuthenticateWithAuthorizationHeader(authorization);
            }

            return null;
        }

        private async Task<AuthenticationTicket> AuthenticateWithApiKeyHeader(string apiKey)
        {
            // Get the user
            var authUser = await Auth.Authenticate(apiKey);
            if (authUser == null)
            {
                // No user was matched
                Logger.WriteWarning("No match for API key.");
                return null;
            }

            var credential = authUser.CredentialUsed;
            var user = authUser.User;

            // Ensure that the user matches the owner scope
            if (!user.MatchesOwnerScope(credential))
            {
                WriteStatus(ServicesStrings.ApiKeyNotAuthorized, 403);
                return null;
            }

            // Set the current user
            Context.Set(ServicesConstants.CurrentUserOwinEnvironmentKey, authUser);

            // Fetch scopes and store them in a claim
            var scopes = JsonConvert.SerializeObject(
                credential.Scopes, Formatting.None);

            // Create authentication ticket
            return new AuthenticationTicket(
                    AuthenticationService.CreateIdentity(
                        user,
                        AuthenticationTypes.ApiKey,
                        // In cases where the apikey in the DB differs from the user provided
                        // value (like apikey.v4) this will hold the hashed value
                        new Claim(NuGetClaims.ApiKey, credential.Value),
                        new Claim(NuGetClaims.Scope, scopes),
                        new Claim(NuGetClaims.CredentialKey, credential.Key.ToString())),
                    new AuthenticationProperties());
        }
        
        private async Task<AuthenticationTicket> AuthenticateWithAuthorizationHeader(string authorizationHeader)
        {
            var authorization = authorizationHeader.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
            if (authorization == null
                || authorization.Length != 2
                || !StringComparer.OrdinalIgnoreCase.Equals("Basic", authorization[0]))
            {
                return null;
            }

            string[] usernamePassword;
            try
            {
                var bytes = Convert.FromBase64String(authorization[1]);
                usernamePassword = Encoding.UTF8.GetString(bytes).Split(new[] { ':' }, 2);
            }
            catch
            {
                usernamePassword = Array.Empty<string>();
            }

            if (usernamePassword.Length != 2
                || string.IsNullOrWhiteSpace(usernamePassword[0])
                || string.IsNullOrWhiteSpace(usernamePassword[1]))
            {
                Context.Set<string>("Message", "The provided Basic authorization credential is not valid.");
                return null;
            }

            if (LooksLikeJwt(usernamePassword[1]))
            {
                return await AuthenticateWithGitHubToken(usernamePassword[0], usernamePassword[1]);
            }

            Context.Set<string>("Message", "The provided Basic authorization password does not appear to be a JWT and will be ignored.");
            return null;
        }

        private bool LooksLikeJwt(string token)
        {
            var tokenPieces = token.Split(new char[] { '.' }, 3);
            if (tokenPieces.Length != 3)
            {
                return false;
            }

            try
            {
                new JwtSecurityTokenHandler().ReadJwtToken(token);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task<AuthenticationTicket> AuthenticateWithGitHubToken(string username, string token)
        {
            // TODO: inject this via DI, maybe a singleton?
            var issuer = "https://token.actions.githubusercontent.com";
            var configurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                issuer + "/.well-known/openid-configuration",
                new OpenIdConnectConfigurationRetriever(),
                new HttpDocumentRetriever());
            var discoveryDocument = await configurationManager.GetConfigurationAsync();
            var signingKeys = discoveryDocument.SigningKeys;

            var validationParameters = new TokenValidationParameters
            {
                RequireExpirationTime = true,
                RequireSignedTokens = true,
                ValidateIssuer = true,
                ValidIssuer = issuer,
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = signingKeys,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(2),
                ValidateAudience = false,
            };

            ClaimsPrincipal principal;
            try
            {
                principal = new JwtSecurityTokenHandler().ValidateToken(
                    token,
                    validationParameters,
                    out var rawValidatedToken);
            }
            catch (SecurityTokenExpiredException ex)
            {
                Context.Set<string>("Message", ex.Message);
                return null;
            }
            catch
            {
                Context.Set<string>("Message", "The provided JWT token is not valid.");
                return null;
            }

            var subject = principal.GetClaimOrDefault(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(subject))
            {
                return null;
            }

            var user = Auth
                .Entities
                .Users
                .Include(x => x.GitHubFederatedTokens)
                .Where(x => x.Username == username)
                .FirstOrDefault();

            if (user == null || !user.GitHubFederatedTokens.Any(x => x.Subject == subject))
            {
                Context.Set<string>("Message", "The token is valid but cannot be used for the provided user account.");
                Context.Set<int>("StatusCode", 403);
                return null;
            }

            return new AuthenticationTicket(
                AuthenticationService.CreateIdentity(
                    user,
                    AuthenticationTypes.ApiKey),
                new AuthenticationProperties());
        }
    }
}
