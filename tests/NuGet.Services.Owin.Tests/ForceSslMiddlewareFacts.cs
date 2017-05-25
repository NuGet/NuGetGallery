// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Owin;
using Moq;
using Xunit;

namespace NuGet.Services.Owin.Tests
{
    public class ForceSslMiddlewareFacts
    {
        public static IEnumerable<string> RedirectingMethods => new[]
        {
            "GET",
            "HEAD",
        };

        public static IEnumerable<string> NonRedirectingMethods => new[]
        {
            "POST",
            "PUT",
            "DELETE",
            "OPTIONS",
            "TRACE",
            "CONNECT",
            "PATCH",
        };

        private static IEnumerable<int> PortsToTest => new[]
        {
            443,
            1234
        };

        public static IEnumerable<object[]> AllowedMethodPorts =>
            from method in RedirectingMethods
            from port in PortsToTest
            select new object[] { method, port };

        [Theory]
        [MemberData(nameof(AllowedMethodPorts))]
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

        public static IEnumerable<object[]> ForbiddenMethodsToTest =>
            from method in NonRedirectingMethods
            select new object[] { method };

        [Theory]
        [MemberData(nameof(ForbiddenMethodsToTest))]
        public async Task ForbidsNonGetHead(string method)
        {
            var context = CreateOwinContext(method, new Uri("http://localhost"));
            var next = CreateOwinMiddleware();

            var middleware = new ForceSslMiddleware(next.Object, 443);
            await middleware.Invoke(context);

            next.Verify(n => n.Invoke(It.IsAny<IOwinContext>()), Times.Never());
            Assert.Equal((int)HttpStatusCode.BadRequest, context.Response.StatusCode);
        }

        private static IEnumerable<(string url, string[] excludedPaths)> UrlsAndExcludesContainingUrls => new []
        {
            ("http://localhost/", new[] { "/" }),
            ("http://localhost/Search/Diag", new[] { "/", "/search/diag" }),
            ("http://localhost/somepath?something=somevalue", new[] { "/", "/SomePath" }),
        };

        public static IEnumerable<object[]> ExclusionsToTest =>
            from method in RedirectingMethods.Concat(NonRedirectingMethods)
            from urlExclusion in UrlsAndExcludesContainingUrls
            select new object[] { method, urlExclusion.url, urlExclusion.excludedPaths };

        [Theory]
        [MemberData(nameof(ExclusionsToTest))]
        public async Task RespectsExclusionList(string method, string url, IEnumerable<string> excludedPaths)
        {
            var uri = new Uri(url);
            var context = CreateOwinContext(method, uri);
            var next = CreateOwinMiddleware();

            var middleware = new ForceSslMiddleware(next.Object, 443, excludedPaths);
            await middleware.Invoke(context);

            next.Verify(n => n.Invoke(It.IsAny<IOwinContext>()), Times.Once());
            Assert.Equal((int)HttpStatusCode.OK, context.Response.StatusCode);
        }

        private static IEnumerable<(string url, string[] excludedPaths)> UrlsAndExcludesNotContainingUrl => new[]
        {
            ("http://localhost/", new[] { "/health" }),
            ("http://localhost/search/diag", new[] { "/" }),
            ("http://localhost/somepath?something=somevalue", new[] { "/", "/SomeOtherPath" }),
        };

        public static IEnumerable<object[]> NonExclusionsToTest =>
            from method in RedirectingMethods
            from urlExclusion in UrlsAndExcludesNotContainingUrl
            select new object[] { method, urlExclusion.url, urlExclusion.excludedPaths };

        [Theory]
        [MemberData(nameof(NonExclusionsToTest))]
        public async Task RedirectsNotExcludedPaths(string method, string url, IEnumerable<string> excludedPaths)
        {
            var uri = new Uri(url);
            var context = CreateOwinContext(method, uri);
            var next = CreateOwinMiddleware();

            var middleware = new ForceSslMiddleware(next.Object, 443, excludedPaths);
            await middleware.Invoke(context);

            next.Verify(n => n.Invoke(It.IsAny<IOwinContext>()), Times.Never());
            Assert.Equal((int)HttpStatusCode.Found, context.Response.StatusCode);
        }

        public static IEnumerable<object[]> NonGetHeadNonExclusionsToTest =>
            from method in NonRedirectingMethods
            from urlExclusion in UrlsAndExcludesNotContainingUrl
            select new object[] { method, urlExclusion.url, urlExclusion.excludedPaths };

        [Theory]
        [MemberData(nameof(NonGetHeadNonExclusionsToTest))]
        public async Task ForbidsNonGetHeadRequestsToNotExcludedPaths(string method, string url, IEnumerable<string> excludedPaths)
        {
            var uri = new Uri(url);
            var context = CreateOwinContext(method, uri);
            var next = CreateOwinMiddleware();

            var middleware = new ForceSslMiddleware(next.Object, 443, excludedPaths);
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
