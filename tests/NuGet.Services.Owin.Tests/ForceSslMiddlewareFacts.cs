// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Owin;
using Moq;
using Xunit;

namespace NuGet.Services.Owin.Tests
{
    public class ForceSslMiddlewareFacts
    {
        [Theory]
        [InlineData("GET", 443)]
        [InlineData("HEAD", 443)]
        [InlineData("GET", 1234)]
        [InlineData("HEAD", 1234)]
        public async Task RedirectsGetHeadToHttps(string method, int sslPort)
        {
            var uri = new Uri("http://localhost:8080/somepath/somedocument?somequery=somevalue");
            var context = CreateOwinContext(method, uri);
            var next = CreateOwinMiddleware();

            var middleware = new ForceSslMiddleware(next.Object, sslPort);
            await middleware.Invoke(context);

            next.Verify(n => n.Invoke(It.IsAny<IOwinContext>()), Times.Never());
            Assert.Equal((int)HttpStatusCode.Found, context.Response.StatusCode);
            Uri targetUri = new Uri(context.Response.Headers["Location"]);
            Assert.Equal(Uri.UriSchemeHttps, targetUri.Scheme);
            Assert.Equal(sslPort, targetUri.Port);
            Assert.Equal(uri.Host, targetUri.Host);
            Assert.Equal(uri.PathAndQuery, targetUri.PathAndQuery);
        }

        [Theory]
        [InlineData("POST")]
        [InlineData("PUT")]
        [InlineData("DELETE")]
        [InlineData("OPTIONS")]
        [InlineData("TRACE")]
        [InlineData("CONNECT")]
        [InlineData("PATCH")]
        public async Task ForbidsNonGetHead(string method)
        {
            var context = CreateOwinContext(method, new Uri("http://localhost"));
            var next = CreateOwinMiddleware();

            var middleware = new ForceSslMiddleware(next.Object, 443);
            await middleware.Invoke(context);

            next.Verify(n => n.Invoke(It.IsAny<IOwinContext>()), Times.Never());
            Assert.Equal((int)HttpStatusCode.BadRequest, context.Response.StatusCode);
        }

        private static IOwinContext CreateOwinContext(string method, Uri uri)
        {
            var ctx = new OwinContext();

            ctx.Request.Scheme = uri.Scheme;
            ctx.Request.Host = HostString.FromUriComponent(uri);
            ctx.Request.PathBase = new PathString("");
            ctx.Request.QueryString = QueryString.FromUriComponent(uri);
            ctx.Request.Path = PathString.FromUriComponent(uri);
            ctx.Request.Method = method;

            // Fill in some values that cause exceptions if not present
            ctx.Set<Action<Action<object>, object>>("server.OnSendingHeaders", (_, __) => { });

            return ctx;
        }

        private static Mock<OwinMiddleware> CreateOwinMiddleware()
        {
            var middleware = new Mock<OwinMiddleware>(new object[] { null });
            middleware.Setup(m => m.Invoke(It.IsAny<OwinContext>())).Returns(Task.FromResult<object>(null));
            return middleware;
        }
    }
}
