// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Owin;
using Microsoft.Owin.Security;
using Microsoft.Owin.Security.Cookies;
using Microsoft.Owin.Security.Interop;
using NuGetGallery.Configuration;
using Owin;

namespace NuGetGallery.Authentication.Providers.Cookie
{
    public class LocalUserAuthenticator : Authenticator
    {
        private readonly IDataProtectionProvider _dataProtectionProvider;

        public LocalUserAuthenticator()
        {
        }

        public LocalUserAuthenticator(IDataProtectionProvider dataProtectionProvider)
        {
            _dataProtectionProvider = dataProtectionProvider ?? throw new ArgumentNullException(nameof(dataProtectionProvider));
        }

        protected override void AttachToOwinApp(IGalleryConfigurationService config, IAppBuilder app)
        {
            app.UseCookieAuthentication(CreateCookieAuthenticationOptions(config.Current.RequireSSL));
            app.SetDefaultSignInAsAuthenticationType(AuthenticationTypes.LocalUser);
        }

        internal CookieAuthenticationOptions CreateCookieAuthenticationOptions(bool requireSsl)
        {
            if (_dataProtectionProvider == null)
            {
                throw new InvalidOperationException("The shared Data Protection provider is required for LocalUser cookie authentication.");
            }

            var protector = _dataProtectionProvider.CreateProtector(
                SharedCookieConstants.DataProtectionMiddlewarePurpose,
                SharedCookieConstants.AuthenticationScheme,
                SharedCookieConstants.DataProtectionFormatPurpose);
            var options = new CookieAuthenticationOptions
            {
                AuthenticationType = SharedCookieConstants.AuthenticationScheme,
                AuthenticationMode = AuthenticationMode.Active,
                CookieName = SharedCookieConstants.CookieName,
                CookiePath = SharedCookieConstants.CookiePath,
                CookieHttpOnly = true,
                CookieSecure = requireSsl ? CookieSecureOption.Always : CookieSecureOption.Never,
                LoginPath = new PathString("/users/account/LogOn"),
                ExpireTimeSpan = SharedCookieConstants.Expiration,
                SlidingExpiration = SharedCookieConstants.SlidingExpiration,
                TicketDataFormat = new AspNetTicketDataFormat(new DataProtectorShim(protector)),
                CookieManager = new Microsoft.Owin.Security.Interop.ChunkingCookieManager(),
            };

            BaseConfig.ApplyToOwinSecurityOptions(options);
            if (!string.Equals(
                options.AuthenticationType,
                SharedCookieConstants.AuthenticationScheme,
                StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The LocalUser authentication type must match the shared cookie scheme.");
            }

            return options;
        }

        protected internal override AuthenticatorConfiguration CreateConfigObject()
        {
            return new AuthenticatorConfiguration
            {
                AuthenticationType = AuthenticationTypes.LocalUser,
                Enabled = false
            };
        }
    }
}