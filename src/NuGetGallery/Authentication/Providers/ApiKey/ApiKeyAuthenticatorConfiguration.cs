using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Web;

namespace NuGetGallery.Authentication.Providers.ApiKey
{
    public class ApiKeyAuthenticatorConfiguration : AuthenticatorConfiguration
    {
        [DefaultValue(Constants.ApiKeyHeaderName)]
        public string HeaderName { get; set; }

        [DefaultValue(NuGetClaims.ApiKey)]
        public string Claim { get; set; }
    }
}