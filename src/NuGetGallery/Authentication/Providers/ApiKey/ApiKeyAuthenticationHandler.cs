using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Owin;
using Microsoft.Owin.Logging;
using Microsoft.Owin.Security;
using Microsoft.Owin.Security.Infrastructure;

namespace NuGetGallery.Authentication.Providers.ApiKey
{
    public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
    {
        private ApiKeyAuthenticationOptions _options;

        protected ILogger Logger { get; set; }
        protected AuthenticationService Auth { get; set; }

        private ApiKeyAuthenticationOptions TheOptions { get { return _options ?? Options; } }

        internal ApiKeyAuthenticationHandler() { }
        public ApiKeyAuthenticationHandler(ILogger logger, AuthenticationService auth)
        {
            Logger = logger;
            Auth = auth;
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
                    WriteStatus(Strings.ApiKeyNotAuthorized, 403);
                }
                else
                {
                    // Keep the 401, but add the authentication information
                    WriteStatus(Strings.ApiKeyRequired, 401);
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

        protected override Task<AuthenticationTicket> AuthenticateCoreAsync()
        {
            var apiKey = Request.Headers[TheOptions.ApiKeyHeaderName];
            if (!String.IsNullOrEmpty(apiKey))
            {
                // Get the user
                var authUser = Auth.Authenticate(CredentialBuilder.CreateV1ApiKey(apiKey));
                if (authUser != null)
                {
                    // Set the current user
                    Context.Set(Constants.CurrentUserOwinEnvironmentKey, authUser);

                    return Task.FromResult(
                        new AuthenticationTicket(
                            AuthenticationService.CreateIdentity(
                                authUser.User, 
                                AuthenticationTypes.ApiKey, 
                                new Claim(NuGetClaims.ApiKey, apiKey)),
                            new AuthenticationProperties()));
                }
                else
                {
                    Logger.WriteWarning("No match for API Key!");
                }
            }
            else
            {
                Logger.WriteVerbose("No API Key Header found in request.");
            }
            return Task.FromResult<AuthenticationTicket>(null);
        }
    }
}
