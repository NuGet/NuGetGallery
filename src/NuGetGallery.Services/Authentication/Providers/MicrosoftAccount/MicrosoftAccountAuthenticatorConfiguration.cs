﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Configuration;
using System.Globalization;
using Microsoft.Owin.Security;
using Microsoft.Owin.Security.MicrosoftAccount;

namespace NuGetGallery.Authentication.Providers.MicrosoftAccount
{
    public class MicrosoftAccountAuthenticatorConfiguration : AuthenticatorConfiguration
    {
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }

        public MicrosoftAccountAuthenticatorConfiguration()
        {
            AuthenticationType = MicrosoftAccountAuthenticator.DefaultAuthenticationType;
        }

        public override void ApplyToOwinSecurityOptions(AuthenticationOptions options)
        {
            base.ApplyToOwinSecurityOptions(options);

            var opts = options as MicrosoftAccountAuthenticationOptions;
            if (opts != null)
            {
                if (String.IsNullOrEmpty(ClientId))
                {
                    throw new ConfigurationErrorsException(String.Format(
                        CultureInfo.CurrentCulture,
                        ServicesStrings.MissingRequiredConfigurationValue,
                        "Auth.MicrosoftAccount.ClientId"));
                }

                opts.ClientId = ClientId;

                if (String.IsNullOrEmpty(ClientSecret))
                {
                    throw new ConfigurationErrorsException(String.Format(
                        CultureInfo.CurrentCulture,
                        ServicesStrings.MissingRequiredConfigurationValue,
                        "Auth.MicrosoftAccount.ClientSecret"));
                }

                opts.ClientSecret = ClientSecret;
            }
        }
    }
}
