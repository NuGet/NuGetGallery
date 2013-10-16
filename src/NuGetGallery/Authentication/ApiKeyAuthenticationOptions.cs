using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Microsoft.Owin.Security;

namespace NuGetGallery.Authentication
{
    public class ApiKeyAuthenticationOptions : AuthenticationOptions
    {
        public string ApiKeyFormName { get; set; }
        public string ApiKeyClaim { get; set; }

        public ApiKeyAuthenticationOptions() : base(AuthenticationTypes.ApiKey) {
            ApiKeyFormName = "apiKey";
            ApiKeyClaim = NuGetClaims.ApiKey;
        }
    }
}