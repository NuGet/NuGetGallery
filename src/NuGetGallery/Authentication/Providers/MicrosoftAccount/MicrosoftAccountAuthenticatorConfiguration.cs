using System;
using System.Collections.Generic;
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
                opts.ClientId = ClientId;
                opts.ClientSecret = ClientSecret;
            }
        }
    }
}
