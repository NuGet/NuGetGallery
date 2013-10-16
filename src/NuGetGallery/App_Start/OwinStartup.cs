using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Owin;
using Microsoft.Owin;
using Microsoft.Owin.Extensions;
using Microsoft.Owin.Diagnostics;
using Microsoft.Owin.Security;
using Microsoft.Owin.Security.Cookies;
using NuGetGallery.Authentication;

[assembly: OwinStartup(typeof(NuGetGallery.OwinStartup))]

namespace NuGetGallery
{
    public class OwinStartup
    {
        // This method is auto-detected by the OWIN pipeline. DO NOT RENAME IT!
        public static void Configuration(IAppBuilder app)
        {
            app.UseCookieAuthentication(new CookieAuthenticationOptions()
            {
                AuthenticationType = AuthenticationTypes.Cookie,
                AuthenticationMode = AuthenticationMode.Active,
                CookieHttpOnly = true,
                LoginPath = "/users/account/logon"
            });
            app.UseApiKeyAuthentication(new ApiKeyAuthenticationOptions()
            {
                AuthenticationType = AuthenticationTypes.ApiKey,
                ApiKeyFormName = Constants.ApiKeyParameterName
            });
        }
    }
}