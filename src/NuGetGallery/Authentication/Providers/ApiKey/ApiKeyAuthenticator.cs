using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using NuGetGallery.Configuration;
using Owin;

namespace NuGetGallery.Authentication.Providers.ApiKey
{
    public class ApiKeyAuthenticator : Authenticator<ApiKeyAuthenticatorConfiguration>
    {
        protected override void AttachToOwinApp(ConfigurationService config, IAppBuilder app)
        {
            app.UseApiKeyAuthentication(new ApiKeyAuthenticationOptions()
            {
                ApiKeyHeaderName = Config.HeaderName,
                ApiKeyClaim = Config.Claim
            });
        }
    }
}