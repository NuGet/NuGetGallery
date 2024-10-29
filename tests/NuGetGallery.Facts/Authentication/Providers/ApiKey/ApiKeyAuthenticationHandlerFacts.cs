// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Owin;
using Microsoft.Owin.Logging;
using Microsoft.Owin.Security;
using Moq;
using Newtonsoft.Json;
using NuGet.Services.Entities;
using NuGetGallery.Framework;
using NuGetGallery.Infrastructure.Authentication;
using Xunit;

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
                handler.OwinContext.Request.Headers[ServicesConstants.ApiKeyHeaderName] = "woozle wuzzle";

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
                    new AuthenticationResponseChallenge(new[] { "blarg" }, new AuthenticationProperties());

                // Act
                var body = await handler.OwinContext.Response.CaptureBodyAsString(async () =>
                    await handler.InvokeApplyResponseChallengeAsync());

                // Assert
                Assert.Equal(Strings.ApiKeyRequired, handler.OwinContext.Response.ReasonPhrase);
                Assert.Equal(Strings.ApiKeyRequired, body);
                Assert.Equal(401, handler.OwinContext.Response.StatusCode);

                var authenticateValues = handler.OwinContext.Response.Headers.GetCommaSeparatedValues("WWW-Authenticate");
                Assert.Contains(
                    "ApiKey realm=\"localhost\"",
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
                handler.OwinContext.Request.Headers[ServicesConstants.ApiKeyHeaderName] = "woozle wuzzle";

                // Act
                var body = await handler.OwinContext.Response.CaptureBodyAsString(async () =>
                    await handler.InvokeApplyResponseChallengeAsync());

                // Assert
                Assert.Equal(Strings.ApiKeyNotAuthorized, handler.OwinContext.Response.ReasonPhrase);
                Assert.Equal(Strings.ApiKeyNotAuthorized, body);
                Assert.Equal(403, handler.OwinContext.Response.StatusCode);
            }
        }

        public class TheAuthenticateCoreAsyncMethod : TestContainer
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
                    ServicesConstants.ApiKeyHeaderName,
                    apiKey.ToString().ToLowerInvariant());

                // Act
                var ticket = await handler.InvokeAuthenticateCoreAsync();

                // Assert
                Assert.Null(ticket);
            }

            [Theory]
            [InlineData(CredentialTypes.ApiKey.V1)]
            [InlineData(CredentialTypes.ApiKey.V2)]
            [InlineData(CredentialTypes.ApiKey.V4)]
            public async Task GivenMatchingApiKey_ItReturnsTicketWithClaims(string apiKeyType)
            {
                // Arrange
                var fakes = Get<Fakes>();
                var user = fakes.User;

                //var user = new User { Username = "theUser", EmailAddress = "confirmed@example.com" };
                var handler = await TestableApiKeyAuthenticationHandler.CreateAsync(new ApiKeyAuthenticationOptions());

                var apiKeyCredential = user.Credentials.First(c => c.Type == apiKeyType);
                apiKeyCredential.Key = 99;

                var plaintextApiKey = apiKeyCredential.Type == CredentialTypes.ApiKey.V4 ? fakes.ApiKeyV4PlaintextValue : apiKeyCredential.Value;

                handler.OwinContext.Request.Headers.Set(
                    ServicesConstants.ApiKeyHeaderName,
                    plaintextApiKey);

                handler.MockAuth.SetupAuth(apiKeyCredential, user, credentialValue: plaintextApiKey);

                // Act
                var ticket = await handler.InvokeAuthenticateCoreAsync();

                // Assert
                Assert.NotNull(ticket);
                Assert.Equal(user.Username, ticket.Identity.GetClaimOrDefault(ClaimTypes.NameIdentifier));
                Assert.Equal(apiKeyCredential.Value, ticket.Identity.GetClaimOrDefault(NuGetClaims.ApiKey));
                Assert.Equal(apiKeyCredential.Key.ToString(), ticket.Identity.GetClaimOrDefault(NuGetClaims.CredentialKey));
                Assert.Equal(JsonConvert.SerializeObject(apiKeyCredential.Scopes, Formatting.None), ticket.Identity.GetClaimOrDefault(NuGetClaims.Scope));
            }

            [Fact]
            public async Task GivenMatchingApiKeyWithNoOwnerScope_ItSetsUserInOwinEnvironment()
            {
                // Arrange
                var user = new User { Username = "theUser", EmailAddress = "confirmed@example.com" };
                TestableApiKeyAuthenticationHandler handler = await TestableApiKeyAuthenticationHandler.CreateAsync(new ApiKeyAuthenticationOptions());
                var apiKeyCredential = new CredentialBuilder().CreateApiKey(Fakes.ExpirationForApiKeyV1, out string plaintextApiKey);

                handler.OwinContext.Request.Headers.Set(
                    ServicesConstants.ApiKeyHeaderName,
                    plaintextApiKey);
                handler.MockAuth.SetupAuth(apiKeyCredential, user, credentialValue: plaintextApiKey);

                // Act
                await handler.InvokeAuthenticateCoreAsync();

                // Assert
                var authUser = Assert.IsType<AuthenticatedUser>(
                    handler.OwinContext.Environment[ServicesConstants.CurrentUserOwinEnvironmentKey]);
                Assert.Same(user, authUser.User);
            }

            [Fact]
            public async Task GivenMatchingApiKeyWithOwnerScopeOfSelf_ItSetsUserInOwinEnvironment()
            {
                // Arrange
                var user = new User { Key = 1234, Username = "theUser", EmailAddress = "confirmed@example.com" };
                TestableApiKeyAuthenticationHandler handler = await TestableApiKeyAuthenticationHandler.CreateAsync(new ApiKeyAuthenticationOptions());
                var apiKeyCredential = new CredentialBuilder().CreateApiKey(Fakes.ExpirationForApiKeyV1, out string plaintextApiKey);
                apiKeyCredential.Scopes.Add(new Scope(1234, "thePackage", "theAction"));

                handler.OwinContext.Request.Headers.Set(
                    ServicesConstants.ApiKeyHeaderName,
                    plaintextApiKey);
                handler.MockAuth.SetupAuth(apiKeyCredential, user, credentialValue: plaintextApiKey);

                // Act
                await handler.InvokeAuthenticateCoreAsync();

                // Assert
                var authUser = Assert.IsType<AuthenticatedUser>(
                    handler.OwinContext.Environment[ServicesConstants.CurrentUserOwinEnvironmentKey]);
                Assert.Same(user, authUser.User);
            }

            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public async Task GivenMatchingApiKeyWithOwnerScopeOfOrganization_ItSetsUserInOwinEnvironment(bool isAdmin)
            {
                // Arrange
                var organization = new Organization() { Key = 2345 };
                var user = new User { Key = 1234, Username = "theUser", EmailAddress = "confirmed@example.com" };
                user.Organizations.Add(new Membership
                {
                    OrganizationKey = 2345,
                    Organization = organization,
                    IsAdmin = isAdmin
                });

                TestableApiKeyAuthenticationHandler handler = await TestableApiKeyAuthenticationHandler.CreateAsync(new ApiKeyAuthenticationOptions());
                var apiKeyCredential = new CredentialBuilder().CreateApiKey(Fakes.ExpirationForApiKeyV1, out string plaintextApiKey);
                apiKeyCredential.Scopes.Add(new Scope(2345, "thePackage", "theAction"));

                handler.OwinContext.Request.Headers.Set(
                    ServicesConstants.ApiKeyHeaderName,
                    plaintextApiKey);
                handler.MockAuth.SetupAuth(apiKeyCredential, user, credentialValue: plaintextApiKey);

                // Act
                await handler.InvokeAuthenticateCoreAsync();

                // Assert
                var authUser = Assert.IsType<AuthenticatedUser>(
                    handler.OwinContext.Environment[ServicesConstants.CurrentUserOwinEnvironmentKey]);
                Assert.Same(user, authUser.User);
            }

            [Fact]
            public async Task GivenApiKeyWithOwnerScopeThatDoesNotMatch_WritesUnauthorizedResponse()
            {
                // Arrange
                var user = new User { Key = 1234, Username = "theUser", EmailAddress = "confirmed@example.com" };
                TestableApiKeyAuthenticationHandler handler = await TestableApiKeyAuthenticationHandler.CreateAsync(new ApiKeyAuthenticationOptions());
                var apiKeyCredential = new CredentialBuilder().CreateApiKey(Fakes.ExpirationForApiKeyV1, out string plaintextApiKey);
                apiKeyCredential.Scopes.Add(new Scope(2345, "thePackage", "theAction"));

                handler.OwinContext.Request.Headers.Set(
                    ServicesConstants.ApiKeyHeaderName,
                    plaintextApiKey);
                handler.MockAuth.SetupAuth(apiKeyCredential, user, credentialValue: plaintextApiKey);

                // Act
                var body = await handler.OwinContext.Response.CaptureBodyAsString(async () =>
                    await handler.InvokeAuthenticateCoreAsync());

                // Assert
                Assert.Equal(Strings.ApiKeyNotAuthorized, handler.OwinContext.Response.ReasonPhrase);
                Assert.Equal(Strings.ApiKeyNotAuthorized, body);
                Assert.Equal(403, handler.OwinContext.Response.StatusCode);
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
                CredentialBuilder = new CredentialBuilder();
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
