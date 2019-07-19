﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Security.Claims;
using System.Threading.Tasks;
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
                if (!String.IsNullOrEmpty(apiKey))
                {
                    // Had an API key, but it didn't match a user
                    WriteStatus(ServicesStrings.ApiKeyNotAuthorized, 403);
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
                // Get the user
                var authUser = await Auth.Authenticate(apiKey);
                if (authUser != null)
                {
                    var credential = authUser.CredentialUsed;
                    var user = authUser.User;

                    // Ensure that the user matches the owner scope
                    if (!user.MatchesOwnerScope(credential))
                    {
                        WriteStatus(ServicesStrings.ApiKeyNotAuthorized, 403);
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
                else
                {
                    // No user was matched
                    Logger.WriteWarning("No match for API Key!");
                }
            }
            else
            {
                Logger.WriteVerbose("No API Key Header found in request.");
            }

            return null;
        }
    }
}
