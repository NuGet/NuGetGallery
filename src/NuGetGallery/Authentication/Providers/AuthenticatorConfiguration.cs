// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Microsoft.Owin.Security;
using NuGetGallery.Configuration;

namespace NuGetGallery.Authentication.Providers
{
    public class AuthenticatorConfiguration
    {
        [DefaultValue(false)]
        public bool Enabled { get; set; }

        public string AuthenticationType { get; set; }

        public IDictionary<string, string> GetConfigValues()
        {
            return ConfigurationService.GetConfigProperties(this)
                .ToDictionary(
                    p => string.IsNullOrEmpty(p.DisplayName) ? p.Name : p.DisplayName,
                    p => p.GetValue(this).ToStringSafe());
        }

        public virtual void ApplyToOwinSecurityOptions(AuthenticationOptions options)
        {
            if (!string.IsNullOrEmpty(AuthenticationType))
            {
                options.AuthenticationType = AuthenticationType;
            }
        }
    }
}