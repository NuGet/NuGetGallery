// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Owin;
using Microsoft.Owin.Security;
using Microsoft.Owin.Security.Cookies;
using NuGetGallery.Configuration;
using Owin;

namespace NuGetGallery.Authentication.Providers.Cookie
{
    public class LocalUserAuthenticator : Authenticator
    {
        protected override void AttachToOwinApp(IGalleryConfigurationService config, IAppBuilder app)
        {
            var cookieSecurity = config.Current.RequireSSL ?
                CookieSecureOption.Always :
                CookieSecureOption.Never;

            var options = new CookieAuthenticationOptions
            {
                AuthenticationType = AuthenticationTypes.LocalUser,
                AuthenticationMode = AuthenticationMode.Active,
                CookieHttpOnly = true,
                CookieSecure = cookieSecurity,
                LoginPath = new PathString("/users/account/LogOn"),
                ExpireTimeSpan = TimeSpan.FromHours(6),
                SlidingExpiration = true
            };

            BaseConfig.ApplyToOwinSecurityOptions(options);
            app.UseCookieAuthentication(options);
            app.SetDefaultSignInAsAuthenticationType(AuthenticationTypes.LocalUser);
        }

        protected override AuthenticatorConfiguration CreateConfigObject()
        {
            return new AuthenticatorConfiguration
            {
                AuthenticationType = AuthenticationTypes.LocalUser,
                Enabled = false
            };
        }
    }
}