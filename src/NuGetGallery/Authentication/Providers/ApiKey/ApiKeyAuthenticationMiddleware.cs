using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Microsoft.Owin;
using Microsoft.Owin.Logging;
using Microsoft.Owin.Security.Infrastructure;
using Owin;

namespace NuGetGallery.Authentication.Providers.ApiKey
{
    public class ApiKeyAuthenticationMiddleware : AuthenticationMiddleware<ApiKeyAuthenticationOptions>
    {
        private ILogger _logger;

        public ApiKeyAuthenticationMiddleware(OwinMiddleware next, IAppBuilder app, ApiKeyAuthenticationOptions options)
            : base(next, options)
        {
            _logger = app.CreateLogger<ILogger>();
        }

        protected override AuthenticationHandler<ApiKeyAuthenticationOptions> CreateHandler()
        {
            return new ApiKeyAuthenticationHandler(
                _logger,
                DependencyResolver.Current.GetService<AuthenticationService>());
        }
    }
}