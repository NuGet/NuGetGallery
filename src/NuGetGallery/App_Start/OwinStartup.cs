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
                AuthenticationMode = AuthenticationMode.Active,
                CookieHttpOnly = true,
                CookieName = System.Web.Security.FormsAuthentication.FormsCookieName,
                LoginPath = "/users/account/logon"
            });
        }
    }
}