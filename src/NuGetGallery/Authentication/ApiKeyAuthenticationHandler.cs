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

        protected override Task<AuthenticationTicket> AuthenticateCoreAsync()
        {
            // Check if we should run based on root path
            if (IsPathMatch(Context.Request.Path))
            {
                var apiKey = Request.Headers[Options.ApiKeyHeaderName];
                if (!String.IsNullOrEmpty(apiKey))
                {
                    // Get the user
                    var user = Auth.Authenticate(CredentialBuilder.CreateV1ApiKey(apiKey));
                    if (user != null)
                    {
                        return Task.FromResult(
                            new AuthenticationTicket(
                                new ResolvedUserIdentity(user, AuthenticationTypes.ApiKey), 
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
            }
            else
            {
                Logger.WriteVerbose("Skipped API Key authentication. Not under specified root path: " + Request.Path);
            }
            return Task.FromResult<AuthenticationTicket>(null);
        }

        protected bool IsPathMatch(string path)
        {
            if (String.IsNullOrEmpty(Options.RootPath))
            {
                return true;
            }

            var root = NormalizeRootPath(Options.RootPath);
            path = String.IsNullOrEmpty(path) ? "/" : path;

            return path.StartsWith(root, StringComparison.OrdinalIgnoreCase);
        }

        private string NormalizeRootPath(string path)
        {
            if (path.StartsWith("~"))
            {
                path = path.Substring(1);
            }
            if (path.EndsWith("/"))
            {
                path = path.Substring(0, path.Length - 1);
            }
            return path;
        }
    }
}
