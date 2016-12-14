// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Configuration;
using System.Globalization;
using Microsoft.Owin.Security;
using Microsoft.Owin.Security.RPS;

namespace NuGetGallery.Authentication.Providers.RPSMicrosoftAccount
{
    public class RPSMicrosoftAccountAuthenticatorConfiguration : AuthenticatorConfiguration
    {
        public string SiteId { get; set; }
        public string AuthPolicy { get; set; }
        public string CookieCertSKI { get; set; }
        public string CookieDomain { get; set; }

        public RPSMicrosoftAccountAuthenticatorConfiguration()
        {
            AuthenticationType = RPSMicrosoftAccountAuthenticator.DefaultAuthenticationType;
        }

        public override void ApplyToOwinSecurityOptions(AuthenticationOptions options)
        {
            base.ApplyToOwinSecurityOptions(options);

            var opts = options as RPSAuthenticationOptions;
            if (opts != null)
            {
                if (String.IsNullOrEmpty(SiteId) || !uint.TryParse(SiteId, out uint siteId))
                {
                    throw new ConfigurationErrorsException(String.Format(
                        CultureInfo.CurrentCulture,
                        Strings.MissingRequiredConfigurationValue,
                        "Auth.RPSMicrosoftAccount.SiteId"));
                }

                opts.SiteId = siteId;

                if (String.IsNullOrEmpty(AuthPolicy))
                {
                    throw new ConfigurationErrorsException(String.Format(
                        CultureInfo.CurrentCulture,
                        Strings.MissingRequiredConfigurationValue,
                        "Auth.RPSMicrosoftAccount.AuthPolicy"));
                }

                opts.AuthenticationPolicy = AuthPolicy;

                if (String.IsNullOrEmpty(CookieDomain))
                {
                    throw new ConfigurationErrorsException(String.Format(
                        CultureInfo.CurrentCulture,
                        Strings.MissingRequiredConfigurationValue,
                        "Auth.RPSMicrosoftAccount.CookieDomain"));
                }

                opts.AuthCookieName = RPSAuthenticationDefaults.AuthCookieName;
                opts.AuthCookieDomain = CookieDomain;
                opts.SecAuthCookieName = RPSAuthenticationDefaults.SecAuthCookieName;
                opts.SecAuthCookieDomain = CookieDomain;

                if (String.IsNullOrEmpty(CookieCertSKI))
                {
                    throw new ConfigurationErrorsException(String.Format(
                        CultureInfo.CurrentCulture,
                        Strings.MissingRequiredConfigurationValue,
                        "Auth.RPSMicrosoftAccount.CookieCertSKI"));
                }

                opts.CookieCertSKI = new string[] { CookieCertSKI };
                opts.LogoutPath = "/";
            }
        }
    }
}
