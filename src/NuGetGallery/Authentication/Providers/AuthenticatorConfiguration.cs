using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Web;
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
                    p => String.IsNullOrEmpty(p.DisplayName) ? p.Name : p.DisplayName,
                    p => p.GetValue(this).ToStringSafe());
        }

        public virtual void ApplyToOwinSecurityOptions(AuthenticationOptions options)
        {
            if (!String.IsNullOrEmpty(AuthenticationType))
            {
                options.AuthenticationType = AuthenticationType;
            }
        }
    }
}