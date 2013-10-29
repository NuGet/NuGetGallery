using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Linq;
using System.Text;
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
                        Strings.MissingRequiredConfigurationValue,
                        "Auth.MicrosoftAccount.ClientId"));
                }

                opts.ClientId = ClientId;

                if (String.IsNullOrEmpty(ClientSecret))
                {
                    throw new ConfigurationErrorsException(String.Format(
                        CultureInfo.CurrentCulture,
                        Strings.MissingRequiredConfigurationValue,
                        "Auth.MicrosoftAccount.ClientSecret"));
                }

                opts.ClientSecret = ClientSecret;
            }
        }
    }
}
