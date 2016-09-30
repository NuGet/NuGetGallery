// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Web.Mvc;
using Microsoft.Owin;
using Microsoft.Owin.Logging;
using Microsoft.Owin.Security.Infrastructure;
using NuGetGallery.Infrastructure.Authentication;
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
                DependencyResolver.Current.GetService<AuthenticationService>(),
                DependencyResolver.Current.GetService<ICredentialBuilder>());
        }
    }
}