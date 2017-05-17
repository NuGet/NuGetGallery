using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Owin;
using Microsoft.Owin.Logging;
using Owin;

namespace NuGetGallery.Authentication
{
    public class ForceSslMiddleware : OwinMiddleware
    {
        private readonly ILogger _logger;
        private readonly int _sslPort;

        public ForceSslMiddleware(OwinMiddleware next, ILogger logger, int sslPort) : base(next)
        {
            _logger = logger;
            _sslPort = sslPort;
        }

        public override async Task Invoke(IOwinContext context)
        {
            if (!context.Request.IsSecure)
            {
                if (context.Request.Method == HttpMethod.Get.Method || context.Request.Method == HttpMethod.Head.Method)
                {
                    context.Response.Redirect(new UriBuilder(context.Request.Uri)
                    {
                        Scheme = Uri.UriSchemeHttps,
                        Port = _sslPort
                    }.Uri.AbsoluteUri);
                }
                else
                {
                    context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                    context.Response.ReasonPhrase = Strings.SSLRequired;
                }
            }
            else
            {
                await Next.Invoke(context);
            }
        }
    }

    public static class ForceSslMiddlewareExtensions
    {
        public static IAppBuilder UseForceSsl(this IAppBuilder appBuilder)
        {
            return UseForceSsl(appBuilder, 443);
        }

        public static IAppBuilder UseForceSsl(this IAppBuilder appBuilder, int sslPort)
        {
            return appBuilder.Use<ForceSslMiddleware>(appBuilder.CreateLogger<ForceSslMiddleware>(), sslPort);
        }
    }
}
