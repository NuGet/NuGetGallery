// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Security.Claims;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Web;
using Moq;
using NuGetGallery.Authentication;
using Xunit;

namespace NuGetGallery.Helpers
{
    public class AuditingHelperFacts
    {
        [Fact]
        public async Task GetAspNetOnBehalfOfAsync_WithoutContext_ReturnsNullForNullHttpContext()
        {
            var actor = await AuditingHelper.GetAspNetOnBehalfOfAsync();

            Assert.Null(actor);
        }

        [Fact]
        public async Task GetAspNetOnBehalfOfAsync_WithContext_ReturnsActor_WithHttpXForwardedForHeader()
        {
            var request = new Mock<HttpRequestBase>();
            var identity = new Mock<IIdentity>();
            var user = new Mock<IPrincipal>();
            var context = new Mock<HttpContextBase>();

            request.SetupGet(x => x.ServerVariables)
                .Returns(new NameValueCollection() { { "HTTP_X_FORWARDED_FOR", "a" } });
            identity.Setup(x => x.Name)
                .Returns("b");
            identity.Setup(x => x.AuthenticationType)
                .Returns("c");
            user.Setup(x => x.Identity)
                .Returns(identity.Object);
            context.Setup(x => x.Request)
                .Returns(request.Object);
            context.Setup(x => x.User)
                .Returns(user.Object);

            var actor = await AuditingHelper.GetAspNetOnBehalfOfAsync(context.Object);

            Assert.NotNull(actor);
            Assert.Equal("c", actor.AuthenticationType);
            Assert.Equal("a", actor.MachineIP);
            Assert.Null(actor.MachineName);
            Assert.Null(actor.OnBehalfOf);
            Assert.InRange(actor.TimestampUtc, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(1));
            Assert.Equal("b", actor.UserName);
        }

        [Fact]
        public async Task GetAspNetOnBehalfOfAsync_WithContext_ReturnsActor_WithRemoteAddrHeader()
        {
            var request = new Mock<HttpRequestBase>();
            var identity = new Mock<IIdentity>();
            var user = new Mock<IPrincipal>();
            var context = new Mock<HttpContextBase>();

            request.SetupGet(x => x.ServerVariables)
                .Returns(new NameValueCollection() { { "REMOTE_ADDR", "a" } });
            identity.Setup(x => x.Name)
                .Returns("b");
            identity.Setup(x => x.AuthenticationType)
                .Returns("c");
            user.Setup(x => x.Identity)
                .Returns(identity.Object);
            context.Setup(x => x.Request)
                .Returns(request.Object);
            context.Setup(x => x.User)
                .Returns(user.Object);

            var actor = await AuditingHelper.GetAspNetOnBehalfOfAsync(context.Object);

            Assert.NotNull(actor);
            Assert.Equal("c", actor.AuthenticationType);
            Assert.Equal("a", actor.MachineIP);
            Assert.Null(actor.MachineName);
            Assert.Null(actor.OnBehalfOf);
            Assert.InRange(actor.TimestampUtc, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(1));
            Assert.Equal("b", actor.UserName);
        }

        [Fact]
        public async Task GetAspNetOnBehalfOfAsync_WithContext_ReturnsActor_WithUserHostAddress()
        {
            var request = new Mock<HttpRequestBase>();
            var identity = new Mock<IIdentity>();
            var user = new Mock<IPrincipal>();
            var context = new Mock<HttpContextBase>();

            request.SetupGet(x => x.ServerVariables)
                .Returns(new NameValueCollection());
            request.SetupGet(x => x.UserHostAddress)
                .Returns("a");
            identity.Setup(x => x.Name)
                .Returns("b");
            identity.Setup(x => x.AuthenticationType)
                .Returns("c");
            user.Setup(x => x.Identity)
                .Returns(identity.Object);
            context.Setup(x => x.Request)
                .Returns(request.Object);
            context.Setup(x => x.User)
                .Returns(user.Object);

            var actor = await AuditingHelper.GetAspNetOnBehalfOfAsync(context.Object);

            Assert.NotNull(actor);
            Assert.Equal("c", actor.AuthenticationType);
            Assert.Equal("a", actor.MachineIP);
            Assert.Null(actor.MachineName);
            Assert.Null(actor.OnBehalfOf);
            Assert.InRange(actor.TimestampUtc, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(1));
            Assert.Equal("b", actor.UserName);
        }

        [Fact]
        public async Task GetAspNetOnBehalfOfAsync_WithContext_ObfuscatesLastIpAddressOctet()
        {
            var request = new Mock<HttpRequestBase>();
            var identity = new Mock<IIdentity>();
            var user = new Mock<IPrincipal>();
            var context = new Mock<HttpContextBase>();

            request.SetupGet(x => x.ServerVariables)
                .Returns(new NameValueCollection() { { "HTTP_X_FORWARDED_FOR", "1.2.3.4" } });
            identity.Setup(x => x.Name)
                .Returns("b");
            identity.Setup(x => x.AuthenticationType)
                .Returns("c");
            user.Setup(x => x.Identity)
                .Returns(identity.Object);
            context.Setup(x => x.Request)
                .Returns(request.Object);
            context.Setup(x => x.User)
                .Returns(user.Object);

            var actor = await AuditingHelper.GetAspNetOnBehalfOfAsync(context.Object);

            Assert.NotNull(actor);
            Assert.Equal("c", actor.AuthenticationType);
            Assert.Equal("1.2.3.0", actor.MachineIP);
            Assert.Null(actor.MachineName);
            Assert.Null(actor.OnBehalfOf);
            Assert.InRange(actor.TimestampUtc, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(1));
            Assert.Equal("b", actor.UserName);
        }

        [Fact]
        public async Task GetAspNetOnBehalfOfAsync_WithContext_ResturnActorWithCredentialKey()
        {
            var request = new Mock<HttpRequestBase>();
            var identity = new Mock<IIdentity>();
            var user = new Mock<IPrincipal>();
            var context = new Mock<HttpContextBase>();

            request.SetupGet(x => x.ServerVariables)
                .Returns(new NameValueCollection() { { "HTTP_X_FORWARDED_FOR", "1.2.3.4" } });
            identity.Setup(x => x.Name)
                .Returns("b");
            identity.Setup(x => x.AuthenticationType)
                .Returns("c");

            var cliamsIdentity = new ClaimsIdentity(identity.Object, new List<Claim> { new Claim(NuGetClaims.CredentialKey, "99") });
            user.Setup(x => x.Identity)
                .Returns(cliamsIdentity);
            context.Setup(x => x.Request)
                .Returns(request.Object);
            context.Setup(x => x.User)
                .Returns(user.Object);

            var actor = await AuditingHelper.GetAspNetOnBehalfOfAsync(context.Object);

            Assert.NotNull(actor);
            Assert.Equal("c", actor.AuthenticationType);
            Assert.Equal("99", actor.CredentialKey);
            Assert.Equal("1.2.3.0", actor.MachineIP);
            Assert.Null(actor.MachineName);
            Assert.Null(actor.OnBehalfOf);
            Assert.InRange(actor.TimestampUtc, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(1));
            Assert.Equal("b", actor.UserName);
        }

        [Fact]
        public async Task GetAspNetOnBehalfOfAsync_WithContext_SupportsNullUser()
        {
            var request = new Mock<HttpRequestBase>();
            var context = new Mock<HttpContextBase>();

            request.SetupGet(x => x.ServerVariables)
                .Returns(new NameValueCollection() { { "HTTP_X_FORWARDED_FOR", "1.2.3.4" } });
            context.Setup(x => x.Request)
                .Returns(request.Object);
            context.Setup(x => x.User)
                .Returns((IPrincipal)null);

            var actor = await AuditingHelper.GetAspNetOnBehalfOfAsync(context.Object);

            Assert.NotNull(actor);
            Assert.Null(actor.AuthenticationType);
            Assert.Equal("1.2.3.0", actor.MachineIP);
            Assert.Null(actor.MachineName);
            Assert.Null(actor.OnBehalfOf);
            Assert.InRange(actor.TimestampUtc, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(1));
            Assert.Null(actor.UserName);
        }
    }
}
