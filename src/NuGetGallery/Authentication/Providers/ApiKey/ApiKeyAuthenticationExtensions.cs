using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using NuGetGallery.Authentication;
using Microsoft.Owin.Extensions;
using NuGetGallery.Authentication.Providers.ApiKey;

namespace Owin
{
    public static class ApiKeyAuthenticationExtensions
    {
        public static IAppBuilder UseApiKeyAuthentication(this IAppBuilder self)
        {
            return UseApiKeyAuthentication(self, new ApiKeyAuthenticationOptions());
        }

        public static IAppBuilder UseApiKeyAuthentication(this IAppBuilder self, ApiKeyAuthenticationOptions options)
        {
            if (self == null)
            {
                throw new ArgumentNullException("self");
            }

            self.Use(typeof(ApiKeyAuthenticationMiddleware), self, options);
            self.UseStageMarker(PipelineStage.Authenticate);
            return self;
        }
    }
}