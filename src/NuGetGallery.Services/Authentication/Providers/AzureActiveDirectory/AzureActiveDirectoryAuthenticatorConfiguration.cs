// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Configuration;
using System.Globalization;
using Microsoft.Owin.Security;
using Microsoft.Owin.Security.OpenIdConnect;

namespace NuGetGallery.Authentication.Providers.AzureActiveDirectory
{
    public class AzureActiveDirectoryAuthenticatorConfiguration : AuthenticatorConfiguration
    {
        public string ClientId { get; set; }
        public string Authority { get; set; }
        public string Issuer { get; set; }
        public bool ShowOnLoginPage { get; set; }

        public AzureActiveDirectoryAuthenticatorConfiguration()
        {
            AuthenticationType = AzureActiveDirectoryAuthenticator.DefaultAuthenticationType;
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
                        ServicesStrings.MissingRequiredConfigurationValue,
                        "Auth.AzureActiveDirectory.ClientId"));
                }

                opts.ClientId = ClientId;

                // Make sure Authority is configured
                if (String.IsNullOrEmpty(Authority))
                {
                    throw new ConfigurationErrorsException(String.Format(
                        CultureInfo.CurrentCulture,
                        ServicesStrings.MissingRequiredConfigurationValue,
                        "Auth.AzureActiveDirectory.Authority"));
                }

                opts.Authority = Authority;

                // Make sure Issuer is configured
                if (String.IsNullOrEmpty(Issuer))
                {
                    throw new ConfigurationErrorsException(String.Format(
                        CultureInfo.CurrentCulture,
                        ServicesStrings.MissingRequiredConfigurationValue,
                        "Auth.AzureActiveDirectory.Issuer"));
                }
            }
        }
    }
}
