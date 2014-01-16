using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Owin;
using Microsoft.Owin.Logging;
using NuGetGallery.Configuration;
using Owin;

namespace NuGetGallery.Authentication
{
    public class ForceSslWhenAuthenticatedMiddleware : OwinMiddleware
    {
        public static readonly string DefaultCookieName = ".ForceSsl";

        private readonly ILogger _logger;
        public string CookieName { get; private set; }
        public int SslPort { get; private set; }

        public ForceSslWhenAuthenticatedMiddleware(OwinMiddleware next, IAppBuilder app, string cookieName, int sslPort)
            : base(next)
        {
            CookieName = cookieName;
            SslPort = sslPort;
            _logger = app.CreateLogger<ForceSslWhenAuthenticatedMiddleware>();
        }

        public override async Task Invoke(IOwinContext context)
        {
            // Check for the SSL Cookie
            var forceSSL = context.Request.Cookies[CookieName];
            if (!String.IsNullOrEmpty(forceSSL) && !context.Request.IsSecure)
            {
                _logger.WriteVerbose("Force SSL Cookie found. Redirecting to SSL");
                // Presence of the cookie is all we care about, value is ignored
                context.Response.Redirect(new UriBuilder(context.Request.Uri)
                {
                    Scheme = Uri.UriSchemeHttps,
                    Port = SslPort
                }.Uri.AbsoluteUri);
            }
            else
            {
                // Invoke the rest of the pipeline
                await Next.Invoke(context);

                var cookieOptions = new CookieOptions() { HttpOnly = true };
                if (context.Authentication.AuthenticationResponseGrant != null)
                {
                    _logger.WriteVerbose("Auth Grant found, writing Force SSL cookie");
                    // We're granting new authentication, so drop a force ssl cookie
                    // for later.
                    context.Response.Cookies.Append(CookieName, "true", cookieOptions);
                }
                else if (context.Authentication.AuthenticationResponseRevoke != null)
                {
                    _logger.WriteVerbose("Auth Revoke found, removing Force SSL cookie");
                    // We're revoking authentication, so remove the force ssl cookie
                    context.Response.Cookies.Delete(CookieName, new CookieOptions()
                    {
                        HttpOnly = true
                    });
                }
            }
        }
    }
}

namespace Owin {
    using NuGetGallery.Authentication;

    public static class ForceSslWhenAuthenticatedExtensions
    {
        public static IAppBuilder UseForceSslWhenAuthenticated(this IAppBuilder self)
        {
            return UseForceSslWhenAuthenticated(
                self,
                ForceSslWhenAuthenticatedMiddleware.DefaultCookieName,
                443);
        }

        public static IAppBuilder UseForceSslWhenAuthenticated(this IAppBuilder self, int sslPort)
        {
            return UseForceSslWhenAuthenticated(
                self,
                ForceSslWhenAuthenticatedMiddleware.DefaultCookieName,
                sslPort);
        }

        public static IAppBuilder UseForceSslWhenAuthenticated(this IAppBuilder self, string cookieName)
        {
            return UseForceSslWhenAuthenticated(
                self,
                cookieName,
                443);
        }

        public static IAppBuilder UseForceSslWhenAuthenticated(this IAppBuilder self, string cookieName, int sslPort)
        {
            return self.Use(typeof(ForceSslWhenAuthenticatedMiddleware), self, cookieName, sslPort);
        }
    }
}