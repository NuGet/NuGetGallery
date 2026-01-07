// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Claims;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Web;
using Moq;
using NuGetGallery.Authentication;
using Xunit;

namespace NuGetGallery.Auditing
{
    public class AuditActorTests
    {
        [Fact]
        public void Constructor_WithoutOnBehalfOf_AcceptsNullValues()
        {
            var actor = new AuditActor(
                machineName: null,
                machineIP: null,
                userName: null,
                authenticationType: null,
                credentialKey: null,
                timeStampUtc: DateTime.MinValue);

            Assert.Null(actor.MachineName);
            Assert.Null(actor.MachineIP);
            Assert.Null(actor.UserName);
            Assert.Null(actor.AuthenticationType);
            Assert.Null(actor.CredentialKey);
        }

        [Fact]
        public void Constructor_WithOnBehalfOf_AcceptsNullValues()
        {
            var actor = new AuditActor(machineName: null,
                machineIP: null,
                userName: null,
                authenticationType: null,
                credentialKey: null,
                timeStampUtc: DateTime.MinValue,
                onBehalfOf: null);

            Assert.Null(actor.MachineName);
            Assert.Null(actor.MachineIP);
            Assert.Null(actor.UserName);
            Assert.Null(actor.AuthenticationType);
            Assert.Null(actor.OnBehalfOf);
            Assert.Null(actor.CredentialKey);
        }

        [Fact]
        public void Constructor_WithoutOnBehalfOf_AcceptsEmptyStringValues()
        {
            var actor = new AuditActor(
                machineName: string.Empty,
                machineIP: string.Empty,
                userName: string.Empty,
                authenticationType: string.Empty,
                credentialKey: string.Empty,
                timeStampUtc: DateTime.MinValue);

            Assert.Equal(string.Empty, actor.MachineName);
            Assert.Equal(string.Empty, actor.MachineIP);
            Assert.Equal(string.Empty, actor.UserName);
            Assert.Equal(string.Empty, actor.AuthenticationType);
        }

        [Fact]
        public void Constructor_WithOnBehalfOf_AcceptsEmptyStringValues()
        {
            var actor = new AuditActor(
                machineName: string.Empty,
                machineIP: string.Empty,
                userName: string.Empty,
                authenticationType: string.Empty,
                credentialKey: string.Empty,
                timeStampUtc: DateTime.MinValue,
                onBehalfOf: null);

            Assert.Equal(string.Empty, actor.MachineName);
            Assert.Equal(string.Empty, actor.MachineIP);
            Assert.Equal(string.Empty, actor.UserName);
            Assert.Equal(string.Empty, actor.AuthenticationType);
        }

        [Fact]
        public void Constructor_WithoutOnBehalfOf_SetsProperties()
        {
            var actor = new AuditActor(
                machineName: "a",
                machineIP: "b",
                userName: "c",
                authenticationType: "d",
                credentialKey: "e",
                timeStampUtc: DateTime.MinValue);

            Assert.Equal("a", actor.MachineName);
            Assert.Equal("b", actor.MachineIP);
            Assert.Equal("c", actor.UserName);
            Assert.Equal("d", actor.AuthenticationType);
            Assert.Equal("e", actor.CredentialKey);
            Assert.Equal(DateTime.MinValue, actor.TimestampUtc);
        }

        [Fact]
        public void Constructor_WithOnBehalfOf_SetsProperties()
        {
            var onBehalfOfActor = new AuditActor(
                machineName: null,
                machineIP: null,
                userName: null,
                authenticationType: null,
                credentialKey: null,
                timeStampUtc: DateTime.MinValue);
            var actor = new AuditActor(
                machineName: "a",
                machineIP: "b",
                userName: "c",
                authenticationType: "d",
                credentialKey: "e",
                timeStampUtc: DateTime.MinValue,
                onBehalfOf: onBehalfOfActor);

            Assert.Equal("a", actor.MachineName);
            Assert.Equal("b", actor.MachineIP);
            Assert.Equal("c", actor.UserName);
            Assert.Equal("d", actor.AuthenticationType);
            Assert.Equal("e", actor.CredentialKey);
            Assert.Equal(DateTime.MinValue, actor.TimestampUtc);
            Assert.Same(onBehalfOfActor, actor.OnBehalfOf);
        }

        [Fact]
        public async Task GetAspNetOnBehalfOfAsync_WithoutContext_ReturnsMachineActorNullHttpContext()
        {
            var actor = await AuditActor.GetAspNetOnBehalfOfAsync();

            var machineActor = await AuditActor.GetCurrentMachineActorAsync();

            Assert.Equal(machineActor.MachineName, actor.MachineName);
            Assert.Equal(machineActor.MachineIP, actor.MachineIP);
            Assert.Equal(machineActor.UserName, actor.UserName);
            Assert.Equal(machineActor.CredentialKey, actor.CredentialKey);
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

            var actor = await AuditActor.GetAspNetOnBehalfOfAsync(context.Object);

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

            var actor = await AuditActor.GetAspNetOnBehalfOfAsync(context.Object);

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

            var actor = await AuditActor.GetAspNetOnBehalfOfAsync(context.Object);

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

            var actor = await AuditActor.GetAspNetOnBehalfOfAsync(context.Object);

            Assert.NotNull(actor);
            Assert.Equal("c", actor.AuthenticationType);
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

            var actor = await AuditActor.GetAspNetOnBehalfOfAsync(context.Object);

            Assert.NotNull(actor);
            Assert.Null(actor.AuthenticationType);
            Assert.Equal("1.2.3.0", actor.MachineIP);
            Assert.Null(actor.MachineName);
            Assert.Null(actor.OnBehalfOf);
            Assert.InRange(actor.TimestampUtc, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(1));
            Assert.Null(actor.UserName);
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
 
            var actor = await AuditActor.GetAspNetOnBehalfOfAsync(context.Object);
 
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
        public async Task GetCurrentMachineActorAsync_WithoutOnBehalfOf()
        {
            var actor = await AuditActor.GetCurrentMachineActorAsync();
            var expectedIpAddress = await AuditActor.GetLocalIpAddressAsync();

            Assert.NotNull(actor);
            Assert.Equal(Environment.MachineName, actor.MachineName);
            Assert.Equal(expectedIpAddress, actor.MachineIP);
            Assert.Equal($@"{Environment.UserDomainName}\{Environment.UserName}", actor.UserName);
            Assert.Equal("MachineUser", actor.AuthenticationType);
            Assert.InRange(actor.TimestampUtc, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(1));
            Assert.Null(actor.OnBehalfOf);
        }

        [Fact]
        public async Task GetCurrentMachineActorAsync_WithOnBehalfOf_AcceptsNull()
        {
            var expectedResult = await AuditActor.GetCurrentMachineActorAsync();
            var actualResult = await AuditActor.GetCurrentMachineActorAsync(onBehalfOf: null);

            Assert.NotNull(expectedResult);
            Assert.NotNull(actualResult);
            Assert.Equal(expectedResult.MachineName, actualResult.MachineName);
            Assert.Equal(expectedResult.MachineIP, actualResult.MachineIP);
            Assert.Equal(expectedResult.UserName, actualResult.UserName);
            Assert.Equal(expectedResult.AuthenticationType, actualResult.AuthenticationType);
            Assert.InRange(actualResult.TimestampUtc, expectedResult.TimestampUtc, expectedResult.TimestampUtc.AddMinutes(1));
            Assert.Null(actualResult.OnBehalfOf);
        }

        [Fact]
        public async Task GetLocalIpAddressAsync_ReturnsAppropriateValueForLocalMachine()
        {
            string expectedIpAddress = null;

            if (NetworkInterface.GetIsNetworkAvailable())
            {
                var entry = await Dns.GetHostEntryAsync(Dns.GetHostName());

                if (entry != null)
                {
                    expectedIpAddress =
                        TryGetAddress(entry.AddressList, AddressFamily.InterNetworkV6) ??
                        TryGetAddress(entry.AddressList, AddressFamily.InterNetwork);
                }
            }

            var actualIpAddress = await AuditActor.GetLocalIpAddressAsync();

            Assert.Equal(expectedIpAddress, actualIpAddress);
        }

        private static string TryGetAddress(IEnumerable<IPAddress> addresses, AddressFamily family)
        {
            return addresses.Where(address => address.AddressFamily == family)
                            .Select(address => address.ToString())
                            .FirstOrDefault();
        }
    }
}