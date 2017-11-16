// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Configuration;
using System.Globalization;
using Microsoft.Owin.Security;
using Microsoft.Owin.Security.OpenIdConnect;

namespace NuGetGallery.Authentication.Providers.CommonAuth
{
    public class CommonAuthAuthenticatorConfiguration : AuthenticatorConfiguration
    {
        public string ClientId { get; set; }
        public string Authority { get; set; }
        public string Tenant { get; set; }
        public string RedirectUri { get; set; }
        public bool ShowOnLoginPage { get; set; }
        public string Issuer { get; set; }

        private const string V2CommonTenant = "common";

        public CommonAuthAuthenticatorConfiguration()
        {
            AuthenticationType = CommonAuthAuthenticator.DefaultAuthenticationType;
        }

        public override void ApplyToOwinSecurityOptions(AuthenticationOptions options)
        {
            base.ApplyToOwinSecurityOptions(options);

            var opts = options as OpenIdConnectAuthenticationOptions;
            if (opts != null)
            {
                // Set passive so that a HTTP 401 does not automatically trigger
                // Azure AD authentication. NuGet uses an explicit challenge to trigger
                // the auth flow.
                opts.AuthenticationMode = AuthenticationMode.Passive;

                // Make sure ClientId is configured
                if (String.IsNullOrEmpty(ClientId))
                {
                    throw new ConfigurationErrorsException(String.Format(
                        CultureInfo.CurrentCulture,
                        Strings.MissingRequiredConfigurationValue,
                        "Auth.CommonAuth.ClientId"));
                }

                opts.ClientId = ClientId;

                // Make sure Authority is configured
                if (String.IsNullOrEmpty(Authority))
                {
                    throw new ConfigurationErrorsException(String.Format(
                        CultureInfo.CurrentCulture,
                        Strings.MissingRequiredConfigurationValue,
                        "Auth.CommonAuth.Authority"));
                }

                if (string.IsNullOrEmpty(Tenant))
                {
                    Tenant = V2CommonTenant;
                }

                opts.Authority = String.Format(CultureInfo.InvariantCulture, Authority, Tenant);
            }
        }
    }
}
