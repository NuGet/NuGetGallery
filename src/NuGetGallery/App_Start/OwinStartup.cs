using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Owin;
using Ninject;
using Microsoft.Owin;
using Microsoft.Owin.Logging;
using Microsoft.Owin.Extensions;
using Microsoft.Owin.Security;
using Microsoft.Owin.Security.Cookies;
using NuGetGallery.Authentication;
using NuGetGallery.Configuration;

[assembly: OwinStartup(typeof(NuGetGallery.OwinStartup))]

namespace NuGetGallery
{
    public class OwinStartup
    {
        // This method is auto-detected by the OWIN pipeline. DO NOT RENAME IT!
        public static void Configuration(IAppBuilder app)
        {
            // Get config
            var config = Container.Kernel.Get<ConfigurationService>();
            var auth = Container.Kernel.Get<AuthenticationService>();
            
            // Configure logging
            app.SetLoggerFactory(new DiagnosticsLoggerFactory());

            if (config.Current.RequireSSL)
            {
                // Put a middleware at the top of the stack to force the user over to SSL
                // if authenticated.
                app.UseForceSslWhenAuthenticated(config.Current.SSLPort);
            }

            // Attach auth providers
            foreach (var provider in auth.Providers)
            {
                provider.Startup(config, app);
            }
        }
    }
}