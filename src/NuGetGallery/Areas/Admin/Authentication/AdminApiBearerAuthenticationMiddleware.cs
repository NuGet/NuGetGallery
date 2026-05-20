// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Owin;
using Microsoft.Owin.Security.Infrastructure;
using Owin;

namespace NuGetGallery.Areas.Admin.Authentication
{
    /// <summary>
    /// OWIN authentication middleware that creates <see cref="AdminApiBearerAuthenticationHandler"/>
    /// instances for Admin API bearer token validation. This is a thin wrapper following the
    /// OWIN authentication framework pattern used by <c>ApiKeyAuthenticationMiddleware</c>.
    /// </summary>
    public class AdminApiBearerAuthenticationMiddleware
        : AuthenticationMiddleware<AdminApiBearerAuthenticationOptions>
    {
        public AdminApiBearerAuthenticationMiddleware(
            OwinMiddleware next,
            IAppBuilder app,
            AdminApiBearerAuthenticationOptions options)
            : base(next, options)
        {
        }

        protected override AuthenticationHandler<AdminApiBearerAuthenticationOptions> CreateHandler()
        {
            return new AdminApiBearerAuthenticationHandler();
        }
    }
}
