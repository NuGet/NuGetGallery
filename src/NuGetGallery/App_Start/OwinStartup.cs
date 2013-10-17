using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Owin;
using Ninject;
using Microsoft.Owin;
using Microsoft.Owin.Extensions;
using Microsoft.Owin.Diagnostics;
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
            
            var config = Container.Kernel.Get<ConfigurationService>();
            var cookieSecurity = config.Current.RequireSSL ? CookieSecureOption.Always : CookieSecureOption.Never;

            app.UseCookieAuthentication(new CookieAuthenticationOptions()
            {
                AuthenticationType = AuthenticationTypes.Cookie,
                AuthenticationMode = AuthenticationMode.Active,
                CookieHttpOnly = true,
                CookieSecure = cookieSecurity,
                LoginPath = "/users/account/LogOn"
            });
            app.UseApiKeyAuthentication();
        }
    }
}