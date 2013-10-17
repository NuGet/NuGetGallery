using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Owin;
using Microsoft.Owin.Logging;
using Microsoft.Owin.Security;
using Microsoft.Owin.Security.Infrastructure;
using Moq;
using Xunit;
using Xunit.Extensions;

namespace NuGetGallery.Authentication
{
    public class ApiKeyAuthenticationHandlerFacts
    {
        public class TheIsPathMatchMethod
        {
            [Theory]
            [InlineData("")]
            [InlineData(null)]
            [InlineData("/a")]
            [InlineData("/a/b")]
            [InlineData("/a/b/c")]
            [InlineData("/a/b/c/d/e")]
            [InlineData("/z")]
            public async Task GivenNoRootPath_AllPathsMatch(string path)
            {
                var handler = await TestableApiKeyAuthenticationHandler.CreateAsync();
                Assert.True(handler.InvokeIsPathMatch(path));
            }

            [Theory]
            [InlineData("/api")]
            [InlineData("/api/v2")]
            [InlineData("/api/v2/Packages")]
            public async Task GivenARootPath_PathsUnderAndIncludingThatRootMatch(string path)
            {
                var handler = await TestableApiKeyAuthenticationHandler.CreateAsync(new ApiKeyAuthenticationOptions()
                {
                    RootPath = "/api"
                });
                Assert.True(handler.InvokeIsPathMatch(path));
            }

            [Theory]
            [InlineData("/api")]
            [InlineData("/api/v2")]
            [InlineData("/api/v2/Packages")]
            public async Task GivenARootPathWithTrailingSlash_PathsUnderAndIncludingThatRootMatch(string path)
            {
                var handler = await TestableApiKeyAuthenticationHandler.CreateAsync(new ApiKeyAuthenticationOptions()
                {
                    RootPath = "/api/"
                });
                Assert.True(handler.InvokeIsPathMatch(path));
            }

            [Theory]
            [InlineData("/api")]
            [InlineData("/api/v2")]
            [InlineData("/api/v2/Packages")]
            public async Task GivenARootPathWithTrailingSlashAndTildeSlash_PathsUnderAndIncludingThatRootMatch(string path)
            {
                var handler = await TestableApiKeyAuthenticationHandler.CreateAsync(new ApiKeyAuthenticationOptions()
                {
                    RootPath = "~/api/"
                });
                Assert.True(handler.InvokeIsPathMatch(path));
            }

            [Theory]
            [InlineData("/api")]
            [InlineData("/api/v2")]
            [InlineData("/api/v2/Packages")]
            public async Task GivenARootPathWithTildeSlash_PathsUnderAndIncludingThatRootMatch(string path)
            {
                var handler = await TestableApiKeyAuthenticationHandler.CreateAsync(new ApiKeyAuthenticationOptions()
                {
                    RootPath = "~/api"
                });
                Assert.True(handler.InvokeIsPathMatch(path));
            }

            [Theory]
            [InlineData("/packages")]
            [InlineData("/")]
            [InlineData("/flarglebargle")]
            public async Task GivenARootPath_PathsNotUnderAndIncludingThatRootMatch(string path)
            {
                var handler = await TestableApiKeyAuthenticationHandler.CreateAsync(new ApiKeyAuthenticationOptions()
                {
                    RootPath = "/api"
                });
                Assert.False(handler.InvokeIsPathMatch(path));
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
            public async Task GivenMatchingApiKey_ItReturnsTicketForResolvedUser()
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
                var id = Assert.IsType<ResolvedUserIdentity>(ticket.Identity);
                Assert.Same(user, id.User);
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

                var ctxt = (handler.MockContext = new Mock<IOwinContext>()).Object;

                handler.MockContext.Setup(c => c.Request).Returns(new Mock<IOwinRequest>().Object);
                handler.MockContext.Setup(c => c.Response).Returns(new Mock<IOwinResponse>().Object);
                handler.MockContext.Setup(c => c.Request.PathBase).Returns("/testroot");
                handler.MockContext.Setup(c => c.Request.Headers).Returns(new HeaderDictionary(new Dictionary<string, string[]>()));

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

            public bool InvokeIsPathMatch(string path)
            {
                return IsPathMatch(path);
            }
        }
    }
}
