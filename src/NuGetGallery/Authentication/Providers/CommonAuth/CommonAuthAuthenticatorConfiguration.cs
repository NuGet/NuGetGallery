﻿// Copyright (c) .NET Foundation. All rights reserved.
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
        public string ClientSecret { get; set; }

        private const string Authority = "https://login.microsoftonline.com/{0}/v2.0";
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

                if (String.IsNullOrEmpty(ClientSecret))
                {
                    throw new ConfigurationErrorsException(String.Format(
                        CultureInfo.CurrentCulture,
                        Strings.MissingRequiredConfigurationValue,
                        "Auth.CommonAuth.ClientSecret"));
                }

                opts.ClientId = ClientId;
                opts.ClientSecret = ClientSecret;
                opts.Authority = String.Format(CultureInfo.InvariantCulture, Authority, V2CommonTenant);
            }
        }
    }
}
