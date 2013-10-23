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

namespace NuGetGallery.Authentication
{
    public class ApiKeyAuthenticationHandlerFacts
    {
        public class TheGetChallengeMessageMethod
        {
            [Fact]
            public async Task GivenANon401ResponseInActiveMode_ItReturnsNull()
            {
                // Arrange
                var handler = await TestableApiKeyAuthenticationHandler.CreateAsync(new ApiKeyAuthenticationOptions()
                {
                    AuthenticationMode = AuthenticationMode.Active
                });
                handler.OwinContext.Response.StatusCode = 200;

                // Act
                var message = handler.GetChallengeMessage();

                // Assert
                Assert.Null(message);
            }

            [Fact]
            public async Task GivenA401ResponseInPassiveModeWithoutMatchingAuthenticationType_ItReturnsNull()
            {
                // Arrange
                var handler = await TestableApiKeyAuthenticationHandler.CreateAsync(new ApiKeyAuthenticationOptions()
                {
                    AuthenticationMode = AuthenticationMode.Passive,
                    AuthenticationType = "blarg"
                });
                handler.OwinContext.Response.StatusCode = 401;
                handler.OwinContext.Authentication.AuthenticationResponseChallenge = 
                    new AuthenticationResponseChallenge(new [] { "flarg" }, new AuthenticationProperties());

                // Act
                var message = handler.GetChallengeMessage();

                // Assert
                Assert.Null(message);
            }

            [Fact]
            public async Task GivenA401ResponseInPassiveModeWithMatchingAuthenticationTypeAndNoHeader_ItReturnsApiKeyRequired()
            {
                // Arrange
                var handler = await TestableApiKeyAuthenticationHandler.CreateAsync(new ApiKeyAuthenticationOptions()
                {
                    AuthenticationMode = AuthenticationMode.Passive,
                    AuthenticationType = "blarg"
                });
                handler.OwinContext.Response.StatusCode = 401;
                handler.OwinContext.Authentication.AuthenticationResponseChallenge = 
                    new AuthenticationResponseChallenge(new [] { "blarg" }, new AuthenticationProperties());
                
                // Act
                var message = handler.GetChallengeMessage();

                // Assert
                Assert.Equal(Strings.ApiKeyRequired, message);
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
                    new AuthenticationResponseChallenge(new [] { "blarg" }, new AuthenticationProperties());
                handler.OwinContext.Request.Headers[Constants.ApiKeyHeaderName] = "woozle wuzzle";

                // Act
                var message = handler.GetChallengeMessage();

                // Assert
                Assert.Equal(Strings.ApiKeyNotAuthorized, message);
            }

            [Fact]
            public async Task GivenA401ResponseInActiveModeAndNoHeader_ItReturnsApiKeyRequired()
            {
                // Arrange
                var handler = await TestableApiKeyAuthenticationHandler.CreateAsync(new ApiKeyAuthenticationOptions()
                {
                    AuthenticationMode = AuthenticationMode.Active,
                    AuthenticationType = "blarg"
                });
                handler.OwinContext.Response.StatusCode = 401;
                handler.OwinContext.Authentication.AuthenticationResponseChallenge = 
                    new AuthenticationResponseChallenge(new [] { "blarg" }, new AuthenticationProperties());

                // Act
                var message = handler.GetChallengeMessage();

                // Assert
                Assert.Equal(Strings.ApiKeyRequired, message);
            }

            [Fact]
            public async Task GivenA401ResponseInActiveModeAndHeader_ItReturnsApiKeyNotAuthorized()
            {
                // Arrange
                var handler = await TestableApiKeyAuthenticationHandler.CreateAsync(new ApiKeyAuthenticationOptions()
                {
                    AuthenticationMode = AuthenticationMode.Active,
                    AuthenticationType = "blarg"
                });
                handler.OwinContext.Response.StatusCode = 401;
                handler.OwinContext.Authentication.AuthenticationResponseChallenge = 
                    new AuthenticationResponseChallenge(new [] { "blarg" }, new AuthenticationProperties());
                handler.OwinContext.Request.Headers[Constants.ApiKeyHeaderName] = "woozle wuzzle";

                // Act
                var message = handler.GetChallengeMessage();

                // Assert
                Assert.Equal(Strings.ApiKeyNotAuthorized, message);
            }
        }

        public class TheAuthenticateCoreAsyncMethod
        {
            [Fact]
            public async Task GivenANonMatchingPath_ItReturnsNull()
            {
                // Arrange
                var handler = await TestableApiKeyAuthenticationHandler.CreateAsync(new ApiKeyAuthenticationOptions()
                {
                    RootPath = "/api"
                });
                handler.OwinContext.Request.Path = new PathString("/packages");
                
                // Act
                var ticket = await handler.InvokeAuthenticateCoreAsync();
                
                // Assert
                Assert.Null(ticket);
            }

            [Fact]
            public async Task GivenNoApiKeyHeader_ItReturnsNull()
            {
                // Arrange
                TestableApiKeyAuthenticationHandler handler = await TestableApiKeyAuthenticationHandler.CreateAsync(new ApiKeyAuthenticationOptions()
                {
                    RootPath = "/api"
                });
                handler.OwinContext.Request.Path = new PathString("/api/v2/packages");
                
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
                TestableApiKeyAuthenticationHandler handler = await TestableApiKeyAuthenticationHandler.CreateAsync(new ApiKeyAuthenticationOptions()
                {
                    RootPath = "/api"
                });
                handler.OwinContext.Request.Path = new PathString("/api/v2/packages");
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
                TestableApiKeyAuthenticationHandler handler = await TestableApiKeyAuthenticationHandler.CreateAsync(new ApiKeyAuthenticationOptions()
                {
                    RootPath = "/api"
                });
                handler.OwinContext.Request.Path = new PathString("/api/v2/packages");
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
                TestableApiKeyAuthenticationHandler handler = await TestableApiKeyAuthenticationHandler.CreateAsync(new ApiKeyAuthenticationOptions()
                {
                    RootPath = "/api"
                });
                handler.OwinContext.Request.Path = new PathString("/api/v2/packages");
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

                // Grr, have to make an internal call to initialize...
                await (Task)(typeof(AuthenticationHandler<ApiKeyAuthenticationOptions>)
                    .InvokeMember(
                        "Initialize",
                        BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.InvokeMethod,
                        Type.DefaultBinder,
                        handler,
                        new object[] { options, ctxt }));
                return handler;
            }

            public Task<AuthenticationTicket> InvokeAuthenticateCoreAsync()
            {
                return AuthenticateCoreAsync();
            }
        }
    }
}
