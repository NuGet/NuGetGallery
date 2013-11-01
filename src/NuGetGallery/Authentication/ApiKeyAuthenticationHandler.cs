using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Owin.Logging;
using Microsoft.Owin.Security;
using Microsoft.Owin.Security.Infrastructure;

namespace NuGetGallery.Authentication
{
    public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
    {
        protected ILogger Logger { get; set; }
        protected AuthenticationService Auth { get; set; }

        internal ApiKeyAuthenticationHandler() { }
        public ApiKeyAuthenticationHandler(ILogger logger, AuthenticationService auth)
        {
            Logger = logger;
            Auth = auth;
        }

        protected override async Task ApplyResponseChallengeAsync()
        {
            string message = GetChallengeMessage();

            if (message != null)
            {
                Response.ReasonPhrase = message;
                Response.Write(message);
            }
            else
            {
                await base.ApplyResponseChallengeAsync();
            }
        }

        internal string GetChallengeMessage()
        {
            string message = null;
            if (Response.StatusCode == 401 && (Helper.LookupChallenge(Options.AuthenticationType, Options.AuthenticationMode) != null))
            {
                var apiKey = Request.Headers[Options.ApiKeyHeaderName];
                message = Strings.ApiKeyRequired;
                if (!String.IsNullOrEmpty(apiKey))
                {
                    // Had an API key, but it wasn't valid
                    message = Strings.ApiKeyNotAuthorized;
                }

            }
            return message;
        }

        protected override Task<AuthenticationTicket> AuthenticateCoreAsync()
        {
            var apiKey = Request.Headers[Options.ApiKeyHeaderName];
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
