// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Owin;
using Microsoft.Owin.Logging;
using Microsoft.Owin.Security;
using Microsoft.Owin.Security.Infrastructure;
using Moq;
using NuGetGallery.Framework;
using Xunit;
using Xunit.Extensions;

namespace NuGetGallery.Authentication.Providers.ApiKey
{
    public class ApiKeyAuthenticationHandlerFacts
    {
        public class TheApplyResponseChallengeAsyncMethod
        {
            [Fact]
            public async Task GivenANon401ResponseInActiveMode_ItPassesThrough()
            {
                // Arrange
                var handler = await TestableApiKeyAuthenticationHandler.CreateAsync(new ApiKeyAuthenticationOptions()
                {
                    AuthenticationMode = AuthenticationMode.Active
                });
                handler.OwinContext.Response.StatusCode = 200;

                // Act
                await handler.InvokeApplyResponseChallengeAsync();

                // Assert
                Assert.Equal(200, handler.OwinContext.Response.StatusCode);
            }

            [Fact]
            public async Task GivenA401ResponseInPassiveModeWithoutMatchingAuthenticationType_ItPassesThrough()
            {
                // Arrange
                var handler = await TestableApiKeyAuthenticationHandler.CreateAsync(new ApiKeyAuthenticationOptions()
                {
                    AuthenticationMode = AuthenticationMode.Passive,
                    AuthenticationType = "blarg"
                });
                handler.OwinContext.Response.StatusCode = 401;
                handler.OwinContext.Authentication.AuthenticationResponseChallenge =
                    new AuthenticationResponseChallenge(new[] { "flarg" }, new AuthenticationProperties());

                // Act
                await handler.InvokeApplyResponseChallengeAsync();

                // Assert
                Assert.Equal(401, handler.OwinContext.Response.StatusCode);
            }

            [Fact]
            public async Task GivenA401ResponseInPassiveModeWithMatchingAuthenticationTypeAndNoHeader_ItWrites401WithApiKeyRequiredMessage()
            {
                // Arrange
                var handler = await TestableApiKeyAuthenticationHandler.CreateAsync(new ApiKeyAuthenticationOptions()
                {
                    AuthenticationMode = AuthenticationMode.Passive,
                    AuthenticationType = "blarg"
                });
                handler.OwinContext.Response.StatusCode = 401;
                handler.OwinContext.Authentication.AuthenticationResponseChallenge =
                    new AuthenticationResponseChallenge(new[] { "blarg" }, new AuthenticationProperties());

                // Act
                var body = await handler.OwinContext.Response.CaptureBodyAsString(async () =>
                    await handler.InvokeApplyResponseChallengeAsync());

                // Assert
                Assert.Equal(Strings.ApiKeyRequired, handler.OwinContext.Response.ReasonPhrase);
                Assert.Equal(Strings.ApiKeyRequired, body);
                Assert.Equal(401, handler.OwinContext.Response.StatusCode);
            }

            [Fact]
            public async Task GivenA401ResponseInPassiveModeWithMatchingAuthenticationTypeAndHeader_ItReturnsApiKeyNotAuthorized()
            {
                // Arrange
                var handler = await TestableApiKeyAuthenticationHandler.CreateAsync(new ApiKeyAuthenticationOptions()
                {
                    AuthenticationMode = AuthenticationMode.Passive,
                    AuthenticationType = "blarg"
                });
                handler.OwinContext.Response.StatusCode = 401;
                handler.OwinContext.Authentication.AuthenticationResponseChallenge =
                    new AuthenticationResponseChallenge(new[] { "blarg" }, new AuthenticationProperties());
                handler.OwinContext.Request.Headers[Constants.ApiKeyHeaderName] = "woozle wuzzle";

                // Act
                var body = await handler.OwinContext.Response.CaptureBodyAsString(async () =>
                    await handler.InvokeApplyResponseChallengeAsync());

                // Assert
                Assert.Equal(Strings.ApiKeyNotAuthorized, handler.OwinContext.Response.ReasonPhrase);
                Assert.Equal(Strings.ApiKeyNotAuthorized, body);
                Assert.Equal(403, handler.OwinContext.Response.StatusCode);
            }

            [Fact]
            public async Task GivenA401ResponseInActiveModeAndNoHeader_ItReturns401ApiKeyRequired()
            {
                // Arrange
                var handler = await TestableApiKeyAuthenticationHandler.CreateAsync(new ApiKeyAuthenticationOptions()
                {
                    AuthenticationMode = AuthenticationMode.Active,
                    AuthenticationType = "blarg"
                });
                handler.OwinContext.Response.StatusCode = 401;
                handler.OwinContext.Response.Headers.Set("WWW-Authenticate", "existing");
                handler.OwinContext.Authentication.AuthenticationResponseChallenge = 
                    new AuthenticationResponseChallenge(new [] { "blarg" }, new AuthenticationProperties());

                // Act
                var body = await handler.OwinContext.Response.CaptureBodyAsString(async () =>
                    await handler.InvokeApplyResponseChallengeAsync());

                // Assert
                Assert.Equal(Strings.ApiKeyRequired, handler.OwinContext.Response.ReasonPhrase);
                Assert.Equal(Strings.ApiKeyRequired, body);
                Assert.Equal(401, handler.OwinContext.Response.StatusCode);

                var authenticateValues = handler.OwinContext.Response.Headers.GetCommaSeparatedValues("WWW-Authenticate");
                Assert.Contains(
                    "ApiKey realm=\"nuget.local\"", 
                    authenticateValues);
                Assert.Contains(
                    "existing",
                    authenticateValues);
            }

            [Fact]
            public async Task GivenA401ResponseInActiveModeAndHeader_ItReturns403ApiKeyNotAuthorized()
            {
                // Arrange
                var handler = await TestableApiKeyAuthenticationHandler.CreateAsync(new ApiKeyAuthenticationOptions()
                {
                    AuthenticationMode = AuthenticationMode.Active,
                    AuthenticationType = "blarg"
                });
                handler.OwinContext.Response.StatusCode = 401;
                handler.OwinContext.Authentication.AuthenticationResponseChallenge =
                    new AuthenticationResponseChallenge(new[] { "blarg" }, new AuthenticationProperties());
                handler.OwinContext.Request.Headers[Constants.ApiKeyHeaderName] = "woozle wuzzle";

                // Act
                var body = await handler.OwinContext.Response.CaptureBodyAsString(async () =>
                    await handler.InvokeApplyResponseChallengeAsync());

                // Assert
                Assert.Equal(Strings.ApiKeyNotAuthorized, handler.OwinContext.Response.ReasonPhrase);
                Assert.Equal(Strings.ApiKeyNotAuthorized, body);
                Assert.Equal(403, handler.OwinContext.Response.StatusCode);
            }
        }

        public class TheAuthenticateCoreAsyncMethod
        {
            [Fact]
            public async Task GivenNoApiKeyHeader_ItReturnsNull()
            {
                // Arrange
                TestableApiKeyAuthenticationHandler handler = await TestableApiKeyAuthenticationHandler.CreateAsync(new ApiKeyAuthenticationOptions());
                
                // Act
                var ticket = await handler.InvokeAuthenticateCoreAsync();

                // Assert
                Assert.Null(ticket);
            }

            [Fact]
            public async Task GivenNoUserMatchingApiKey_ItReturnsNull()
            {
                // Arrange
                Guid apiKey = Guid.NewGuid();
                TestableApiKeyAuthenticationHandler handler = await TestableApiKeyAuthenticationHandler.CreateAsync(new ApiKeyAuthenticationOptions());
                handler.OwinContext.Request.Headers.Set(
                    Constants.ApiKeyHeaderName,
                    apiKey.ToString().ToLowerInvariant());

                // Act
                var ticket = await handler.InvokeAuthenticateCoreAsync();

                // Assert
                Assert.Null(ticket);
            }

            [Fact]
            public async Task GivenMatchingApiKey_ItReturnsTicketWithUserNameAndRoles()
            {
                // Arrange
                Guid apiKey = Guid.NewGuid();
                var user = new User() { Username = "theUser", EmailAddress = "confirmed@example.com" };
                TestableApiKeyAuthenticationHandler handler = await TestableApiKeyAuthenticationHandler.CreateAsync(new ApiKeyAuthenticationOptions());
                handler.OwinContext.Request.Headers.Set(
                    Constants.ApiKeyHeaderName,
                    apiKey.ToString().ToLowerInvariant());
                handler.MockAuth.SetupAuth(CredentialBuilder.CreateV1ApiKey(apiKey), user);

                // Act
                var ticket = await handler.InvokeAuthenticateCoreAsync();

                // Assert
                Assert.NotNull(ticket);
                Assert.Equal(apiKey.ToString().ToLower(), ticket.Identity.GetClaimOrDefault(NuGetClaims.ApiKey));
            }

            [Fact]
            public async Task GivenMatchingApiKey_ItSetsUserInOwinEnvironment()
            {
                // Arrange
                Guid apiKey = Guid.NewGuid();
                var user = new User() { Username = "theUser", EmailAddress = "confirmed@example.com" };
                TestableApiKeyAuthenticationHandler handler = await TestableApiKeyAuthenticationHandler.CreateAsync(new ApiKeyAuthenticationOptions());
                handler.OwinContext.Request.Headers.Set(
                    Constants.ApiKeyHeaderName,
                    apiKey.ToString().ToLowerInvariant());
                handler.MockAuth.SetupAuth(CredentialBuilder.CreateV1ApiKey(apiKey), user);

                // Act
                await handler.InvokeAuthenticateCoreAsync();

                // Assert
                var authUser = Assert.IsType<AuthenticatedUser>(
                    handler.OwinContext.Environment[Constants.CurrentUserOwinEnvironmentKey]);
                Assert.Same(user, authUser.User);
            }
        }

        // Why a TestableNNN class? Because we need to access protected members.
        public class TestableApiKeyAuthenticationHandler : ApiKeyAuthenticationHandler
        {
            public Mock<AuthenticationService> MockAuth { get; private set; }
            public Mock<ILogger> MockLogger { get; private set; }
            public IOwinContext OwinContext { get { return base.Context; } }

            private TestableApiKeyAuthenticationHandler()
            {
                Logger = (MockLogger = new Mock<ILogger>()).Object;
                Auth = (MockAuth = new Mock<AuthenticationService>()).Object;
            }

            public static Task<TestableApiKeyAuthenticationHandler> CreateAsync()
            {
                return CreateAsync(new ApiKeyAuthenticationOptions());
            }

            public static async Task<TestableApiKeyAuthenticationHandler> CreateAsync(ApiKeyAuthenticationOptions options)
            {
                // Always use passive mode for tests
                options.AuthenticationMode = AuthenticationMode.Passive;

                var handler = new TestableApiKeyAuthenticationHandler();

                var ctxt = Fakes.CreateOwinContext();

                await handler.InitializeAsync(options, ctxt);

                return handler;
            }

            public Task<AuthenticationTicket> InvokeAuthenticateCoreAsync()
            {
                return AuthenticateCoreAsync();
            }

            public Task InvokeApplyResponseChallengeAsync()
            {
                return ApplyResponseChallengeAsync();
            }
        }
    }
}
