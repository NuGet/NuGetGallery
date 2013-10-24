using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Microsoft.Owin.Security;

namespace NuGetGallery.Authentication.Providers.ApiKey
{
    public class ApiKeyAuthenticationOptions : AuthenticationOptions
    {
        public string ApiKeyHeaderName { get; set; }
        public string ApiKeyClaim { get; set; }
        
        public ApiKeyAuthenticationOptions() : base(AuthenticationTypes.ApiKey) {
            ApiKeyHeaderName = Constants.ApiKeyHeaderName;
            ApiKeyClaim = NuGetClaims.ApiKey;
        }
    }
}