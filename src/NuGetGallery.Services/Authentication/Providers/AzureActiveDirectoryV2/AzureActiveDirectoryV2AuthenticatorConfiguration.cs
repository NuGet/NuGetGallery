// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Configuration;
using System.Globalization;
using Microsoft.Owin.Security;
using Microsoft.Owin.Security.OpenIdConnect;

namespace NuGetGallery.Authentication.Providers.AzureActiveDirectoryV2
{
    public class AzureActiveDirectoryV2AuthenticatorConfiguration : AuthenticatorConfiguration
    {
        public string ClientId { get; set; }

        public AzureActiveDirectoryV2AuthenticatorConfiguration()
        {
            AuthenticationType = AzureActiveDirectoryV2Authenticator.DefaultAuthenticationType;
        }

        public override void ApplyToOwinSecurityOptions(AuthenticationOptions options)
        {
            base.ApplyToOwinSecurityOptions(options);

            var openIdOptions = options as OpenIdConnectAuthenticationOptions;
            if (openIdOptions != null)
            {
                // Set passive so that a HTTP 401 does not automatically trigger
                // Microsoft Entra ID authentication. NuGet uses an explicit challenge to trigger
                // the auth flow.
                openIdOptions.AuthenticationMode = AuthenticationMode.Passive;

                // Make sure ClientId is configured
                if (String.IsNullOrEmpty(ClientId))
                {
                    throw new ConfigurationErrorsException(String.Format(
                        CultureInfo.CurrentCulture,
                        ServicesStrings.MissingRequiredConfigurationValue,
                        "Auth.CommonAuth.ClientId"));
                }

                openIdOptions.ClientId = ClientId;
                openIdOptions.Authority = String.Format(CultureInfo.InvariantCulture, AzureActiveDirectoryV2Authenticator.Authority, AzureActiveDirectoryV2Authenticator.V2CommonTenant);
            }
        }
    }
}
