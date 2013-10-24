using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Microsoft.Owin;
using Microsoft.Owin.Security;
using Microsoft.Owin.Security.Cookies;
using NuGetGallery.Configuration;
using Owin;

namespace NuGetGallery.Authentication.Providers.Cookie
{
    public class CookieAuthenticationProvider : AuthenticationProvider
    {
        protected override void AttachToOwinApp(ConfigurationService config, IAppBuilder app)
        {
            var cookieSecurity = config.Current.RequireSSL ? 
                CookieSecureOption.Always : 
                CookieSecureOption.Never;

            var options = new CookieAuthenticationOptions()
            {
                AuthenticationType = AuthenticationTypes.Password,
                CookieHttpOnly = true,
                CookieSecure = cookieSecurity,
                LoginPath = new PathString("/users/account/LogOn")
            };
            
            BaseConfig.ApplyToOwinSecurityOptions(options);
            app.UseCookieAuthentication(options);
        }
    }
}