// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Owin;
using Microsoft.Owin.Builder;
using Microsoft.Owin.Security;
using Moq;
using NuGetGallery.Framework;
using Xunit;

namespace NuGetGallery.Authentication
{
    public class ForceSslWhenAuthenticatedMiddlewareFacts
    {
        [Fact]
        public async Task GivenAForceSslCookieAndNonSslRequest_ItRedirectsToSSL()
        {
            // Arrange
            var context = Fakes.CreateOwinContext();
            var next = Fakes.CreateOwinMiddleware();
            var app = new AppBuilder();
            context.Request
                .SetUrl("http://nuget.local/foo/bar/baz?qux=qooz")
                .SetCookie("ForceSSL", "bogus");
            var middleware = new ForceSslWhenAuthenticatedMiddleware(next.Object, app, "ForceSSL", 443);

            // Act
            await middleware.Invoke(context);

            // Assert
            next.Verify(n => n.Invoke(It.IsAny<IOwinContext>()), Times.Never());
            OwinAssert.WillRedirect(context, "https://nuget.local/foo/bar/baz?qux=qooz");
        }

        [Fact]
        public async Task GivenANonStandardSslPort_ItSpecifiesPortInUrl()
        {
            // Arrange
            var context = Fakes.CreateOwinContext();
            var next = Fakes.CreateOwinMiddleware();
            var app = new AppBuilder();
            context.Request
                .SetUrl("http://nuget.local/foo/bar/baz?qux=qooz")
                .SetCookie("ForceSSL", "bogus");
            var middleware = new ForceSslWhenAuthenticatedMiddleware(next.Object, app, "ForceSSL", 44300);

            // Act
            await middleware.Invoke(context);

            // Assert
            next.Verify(n => n.Invoke(It.IsAny<IOwinContext>()), Times.Never());
            OwinAssert.WillRedirect(context, "https://nuget.local:44300/foo/bar/baz?qux=qooz");
        }

        [Fact]
        public async Task GivenAForceSslCookieAndSslRequest_ItPassesThrough()
        {
            // Arrange
            var context = Fakes.CreateOwinContext();
            var next = Fakes.CreateOwinMiddleware();
            var app = new AppBuilder();
            context.Request
                .SetUrl("https://nuget.local/foo/bar/baz?qux=qooz")
                .SetCookie("ForceSSL", "bogus");
            var middleware = new ForceSslWhenAuthenticatedMiddleware(next.Object, app, "ForceSSL", 443);

            // Act
            await middleware.Invoke(context);

            // Assert
            next.Verify(n => n.Invoke(It.IsAny<IOwinContext>()));
        }

        [Fact]
        public async Task GivenNoForceSslCookieAndNonSslRequest_ItPassesThrough()
        {
            // Arrange
            var context = Fakes.CreateOwinContext();
            var next = Fakes.CreateOwinMiddleware();
            var app = new AppBuilder();
            context.Request
                .SetUrl("http://nuget.local/foo/bar/baz?qux=qooz");
            var middleware = new ForceSslWhenAuthenticatedMiddleware(next.Object, app, "ForceSSL", 443);

            // Act
            await middleware.Invoke(context);

            // Assert
            next.Verify(n => n.Invoke(It.IsAny<IOwinContext>()));
        }

        [Fact]
        public async Task GivenNextMiddlewareGrantsAuth_ItDropsForceSslCookie()
        {
            // Arrange
            var context = Fakes.CreateOwinContext();
            var next = Fakes.CreateOwinMiddleware();
            var app = new AppBuilder();
            var grant = new AuthenticationResponseGrant(new ClaimsIdentity(), new AuthenticationProperties());

            next.Setup(n => n.Invoke(context))
                .Returns<IOwinContext>(c =>
                {
                    c.Authentication.AuthenticationResponseGrant = grant;
                    return Task.FromResult<object>(null);
                });
            context.Request
                .SetUrl("http://nuget.local/foo/bar/baz?qux=qooz");
            var middleware = new ForceSslWhenAuthenticatedMiddleware(next.Object, app, "ForceSSL", 443);

            // Act
            await middleware.Invoke(context);

            // Assert
            OwinAssert.SetsCookie(context.Response, "ForceSSL", "true");
        }

        [Fact]
        public async Task GivenNextMiddlewareRevokesAuth_ItRemovesForceSslCookie()
        {
            // Arrange
            var context = Fakes.CreateOwinContext();
            var next = Fakes.CreateOwinMiddleware();
            var app = new AppBuilder();
            var revoke = new AuthenticationResponseRevoke(new string[0]);

            next.Setup(n => n.Invoke(context))
                .Returns<IOwinContext>(c =>
                {
                    c.Authentication.AuthenticationResponseRevoke = revoke;
                    return Task.FromResult<object>(null);
                });
            context.Request
                .SetUrl("http://nuget.local/foo/bar/baz?qux=qooz");
            var middleware = new ForceSslWhenAuthenticatedMiddleware(next.Object, app, "ForceSSL", 443);

            // Act
            await middleware.Invoke(context);

            // Assert
            OwinAssert.DeletesCookie(context.Response, "ForceSSL");
        }
    }
}
