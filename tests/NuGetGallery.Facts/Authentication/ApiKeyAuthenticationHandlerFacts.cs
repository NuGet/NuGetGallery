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
                handler.MockContext.Setup(c => c.Response.StatusCode).Returns(200);

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
                handler.MockContext
                    .Setup(c => c.Response.StatusCode)
                    .Returns(401);
                handler.MockContext
                    .Setup(c => c.Authentication.AuthenticationResponseChallenge)
                    .Returns(new AuthenticationResponseChallenge(new [] { "flarg" }, new AuthenticationProperties()));

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
                handler.MockContext
                    .Setup(c => c.Response.StatusCode)
                    .Returns(401);
                handler.MockContext
                    .Setup(c => c.Authentication.AuthenticationResponseChallenge)
                    .Returns(new AuthenticationResponseChallenge(new [] { "blarg" }, new AuthenticationProperties()));

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
                handler.MockContext
                    .Setup(c => c.Response.StatusCode)
                    .Returns(401);
                handler.MockContext
                    .Setup(c => c.Authentication.AuthenticationResponseChallenge)
                    .Returns(new AuthenticationResponseChallenge(new [] { "blarg" }, new AuthenticationProperties()));
                handler.MockContext.Object.Request.Headers[Constants.ApiKeyHeaderName] = "woozle wuzzle";

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
                handler.MockContext
                    .Setup(c => c.Response.StatusCode)
                    .Returns(401);
                handler.MockContext
                    .Setup(c => c.Authentication.AuthenticationResponseChallenge)
                    .Returns(new AuthenticationResponseChallenge(new [] { "blarg" }, new AuthenticationProperties()));

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
                handler.MockContext
                    .Setup(c => c.Response.StatusCode)
                    .Returns(401);
                handler.MockContext
                    .Setup(c => c.Authentication.AuthenticationResponseChallenge)
                    .Returns(new AuthenticationResponseChallenge(new [] { "blarg" }, new AuthenticationProperties()));
                handler.MockContext.Object.Request.Headers[Constants.ApiKeyHeaderName] = "woozle wuzzle";

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
                handler.MockContext.Setup(c => c.Request.Path).Returns("/packages");
                
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
                handler.MockContext.Setup(c => c.Request.Path).Returns("/api/v2/packages");
                
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
                handler.MockContext.Setup(c => c.Request.Path).Returns("/api/v2/packages");
                handler.MockContext.Object.Request.Headers.Set(
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
                handler.MockContext.Setup(c => c.Request.Path).Returns("/api/v2/packages");
                handler.MockContext.Object.Request.Headers.Set(
                    Constants.ApiKeyHeaderName,
                    apiKey.ToString().ToLowerInvariant());
                handler.MockAuth.SetupAuth(CredentialBuilder.CreateV1ApiKey(apiKey), user);

                // Act
                var ticket = await handler.InvokeAuthenticateCoreAsync();

                // Assert
                Assert.NotNull(ticket);
                Assert.Equal("theUser", ticket.Identity.GetClaimOrDefault(ClaimTypes.Name));
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
                handler.MockContext.Setup(c => c.Request.Path).Returns("/api/v2/packages");
                handler.MockContext.Object.Request.Headers.Set(
                    Constants.ApiKeyHeaderName,
                    apiKey.ToString().ToLowerInvariant());
                handler.MockAuth.SetupAuth(CredentialBuilder.CreateV1ApiKey(apiKey), user);

                // Act
                await handler.InvokeAuthenticateCoreAsync();

                // Assert
                Assert.Same(user, handler.MockContext.Object.Environment[Constants.CurrentUserOwinEnvironmentKey]);
            }
        }

        // Why a TestableNNN class? Because we need to access protected members.
        public class TestableApiKeyAuthenticationHandler : ApiKeyAuthenticationHandler
        {
            public Mock<IOwinContext> MockContext { get; private set; }
            public Mock<AuthenticationService> MockAuth { get; private set; }
            public Mock<ILogger> MockLogger { get; private set; }

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

                var ctxt = (handler.MockContext = Fakes.CreateOwinContext()).Object;

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
