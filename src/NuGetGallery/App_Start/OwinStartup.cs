// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Web.Mvc;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Owin;
using Microsoft.Owin.Logging;
using Microsoft.Owin.Security;
using Microsoft.Owin.Security.Cookies;
using Ninject;
using NuGetGallery.Authentication;
using NuGetGallery.Authentication.Providers;
using NuGetGallery.Authentication.Providers.Cookie;
using NuGetGallery.Configuration;
using Owin;

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

            // Setup telemetry
            var instrumentationKey = config.Current.AppInsightsInstrumentationKey;
            if (!string.IsNullOrEmpty(instrumentationKey))
            {
                TelemetryConfiguration.Active.InstrumentationKey = instrumentationKey;
            }

            // Configure logging
            app.SetLoggerFactory(new DiagnosticsLoggerFactory());

            // Remove X-AspNetMvc-Version header
            MvcHandler.DisableMvcResponseHeader = true;

            if (config.Current.RequireSSL)
            {
                // Put a middleware at the top of the stack to force the user over to SSL
                // if authenticated.
                app.UseForceSslWhenAuthenticated(config.Current.SSLPort);
            }

            // Get the local user auth provider, if present and attach it first
            Authenticator localUserAuther;
            if (auth.Authenticators.TryGetValue(Authenticator.GetName(typeof(LocalUserAuthenticator)), out localUserAuther))
            {
                // Configure cookie auth now
                localUserAuther.Startup(config, app);
            }

            // Attach external sign-in cookie middleware
            app.SetDefaultSignInAsAuthenticationType(AuthenticationTypes.External);
            app.UseCookieAuthentication(new CookieAuthenticationOptions()
            {
                AuthenticationType = AuthenticationTypes.External,
                AuthenticationMode = AuthenticationMode.Passive,
                CookieName = ".AspNet." + AuthenticationTypes.External,
                ExpireTimeSpan = TimeSpan.FromMinutes(5)
            });

            // Attach non-cookie auth providers
            var nonCookieAuthers = auth
                .Authenticators
                .Where(p => !String.Equals(
                    p.Key,
                    Authenticator.GetName(typeof(LocalUserAuthenticator)),
                    StringComparison.OrdinalIgnoreCase))
                .Select(p => p.Value);
            foreach (var auther in nonCookieAuthers)
            {
                auther.Startup(config, app);
            }
        }
    }
}