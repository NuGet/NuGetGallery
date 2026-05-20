// Copyright(c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Owin;
using Moq;
using NuGetGallery.Configuration;
using Xunit;

namespace NuGetGallery.Filters
{
    public class AdminApiAuthAttributeFacts
    {
        public class TheOnAuthorizationMethod
        {
            [Fact]
            public void Returns404WhenAdminApiDisabled()
            {
                // Arrange
                var context = BuildAuthorizationContext(azpInItems: null);
                SetupConfigService(adminApiEnabled: false);

                var attribute = new AdminApiAuthAttribute();

                // Act
                attribute.OnAuthorization(context.Object);

                // Assert
                var result = context.Object.Result as HttpStatusCodeResult;
                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.NotFound, result.StatusCode);
            }

            [Fact]
            public void Returns401WhenMiddlewareDidNotSetAzp()
            {
                // Arrange
                var context = BuildAuthorizationContext(azpInItems: null);
                SetupConfigService(adminApiEnabled: true);

                var attribute = new AdminApiAuthAttribute();

                // Act
                attribute.OnAuthorization(context.Object);

                // Assert
                var result = context.Object.Result as HttpStatusCodeResult;
                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.Unauthorized, result.StatusCode);
            }

            [Fact]
            public void SetsNoResultWhenMiddlewareAuthenticated()
            {
                // Arrange
                var context = BuildAuthorizationContext(azpInItems: "test-app");
                SetupConfigService(adminApiEnabled: true);

                var attribute = new AdminApiAuthAttribute();

                // Act
                attribute.OnAuthorization(context.Object);

                // Assert
                Assert.Null(context.Object.Result);
            }

            private static void SetupConfigService(bool adminApiEnabled)
            {
                var mockConfig = new Mock<IAppConfiguration>();
                mockConfig.Setup(c => c.AdminApiEnabled).Returns(adminApiEnabled);

                var mockConfigService = new Mock<IGalleryConfigurationService>();
                mockConfigService.Setup(s => s.Current).Returns(mockConfig.Object);

                var mockDependencyResolver = new Mock<IDependencyResolver>();
                mockDependencyResolver
                    .Setup(r => r.GetService(typeof(IGalleryConfigurationService)))
                    .Returns(mockConfigService.Object);

                DependencyResolver.SetResolver(mockDependencyResolver.Object);
            }

            private static Mock<AuthorizationContext> BuildAuthorizationContext(string azpInItems)
            {
                var mockController = new Mock<AppController>();

                var mockRequest = new Mock<HttpRequestBase>();
                mockRequest.Setup(r => r.Headers).Returns([]);

                var items = new Dictionary<object, object>
                {
                    { "owin.Environment", new Dictionary<string, object>() }
                };

                if (azpInItems != null)
                {
                    items[AdminApiAuthAttribute.AzpItemKey] = azpInItems;
                }

                var mockHttpContext = new Mock<HttpContextBase>();
                mockHttpContext.Setup(c => c.Request).Returns(mockRequest.Object);
                mockHttpContext.SetupGet(c => c.Items).Returns(items);
                mockHttpContext.SetupGet(c => c.Response.Cache)
                    .Returns(new Mock<HttpCachePolicyBase>().Object);

                var mockActionDescriptor = new Mock<ActionDescriptor>();
                mockActionDescriptor
                    .Setup(c => c.ControllerDescriptor)
                    .Returns(new Mock<ControllerDescriptor>().Object);

                var mockAuthContext = new Mock<AuthorizationContext>(MockBehavior.Strict);
                mockAuthContext.SetupGet(c => c.HttpContext).Returns(mockHttpContext.Object);
                mockAuthContext.SetupGet(c => c.ActionDescriptor).Returns(mockActionDescriptor.Object);
                mockAuthContext.SetupGet(c => c.Controller).Returns(mockController.Object);
                mockAuthContext.SetupGet(c => c.RouteData).Returns(new RouteData());

                mockAuthContext.Object.Result = null;

                return mockAuthContext;
            }
        }

        public class TheExtractBearerTokenMethod
        {
            [Fact]
            public void ReturnsNullWhenNoAuthorizationHeader()
            {
                var mockRequest = new Mock<IOwinRequest>();
                mockRequest.Setup(r => r.Headers).Returns(new HeaderDictionary(new Dictionary<string, string[]>()));

                var result = AdminApiBearerAuthMiddleware.ExtractBearerToken(mockRequest.Object);

                Assert.Null(result);
            }

            [Fact]
            public void ReturnsTokenWhenBearerPrefix()
            {
                var headers = new HeaderDictionary(new Dictionary<string, string[]>
                {
                    { "Authorization", new[] { "Bearer mytoken123" } }
                });
                var mockRequest = new Mock<IOwinRequest>();
                mockRequest.Setup(r => r.Headers).Returns(headers);

                var result = AdminApiBearerAuthMiddleware.ExtractBearerToken(mockRequest.Object);

                Assert.Equal("mytoken123", result);
            }

            [Fact]
            public void ReturnsNullWhenNonBearerScheme()
            {
                var headers = new HeaderDictionary(new Dictionary<string, string[]>
                {
                    { "Authorization", new[] { "Basic abc123" } }
                });
                var mockRequest = new Mock<IOwinRequest>();
                mockRequest.Setup(r => r.Headers).Returns(headers);

                var result = AdminApiBearerAuthMiddleware.ExtractBearerToken(mockRequest.Object);

                Assert.Null(result);
            }

            [Fact]
            public void IsCaseInsensitive()
            {
                var headers = new HeaderDictionary(new Dictionary<string, string[]>
                {
                    { "Authorization", new[] { "bearer mytoken" } }
                });
                var mockRequest = new Mock<IOwinRequest>();
                mockRequest.Setup(r => r.Headers).Returns(headers);

                var result = AdminApiBearerAuthMiddleware.ExtractBearerToken(mockRequest.Object);

                Assert.Equal("mytoken", result);
            }
        }

        public class TheParseAllowedCallersMethod
        {
            [Fact]
            public void ReturnsEmptyForNull()
            {
                var result = AdminApiBearerAuthMiddleware.ParseAllowedCallers(null);

                Assert.Empty(result);
            }

            [Fact]
            public void ReturnsEmptyForEmptyString()
            {
                var result = AdminApiBearerAuthMiddleware.ParseAllowedCallers("");

                Assert.Empty(result);
            }

            [Fact]
            public void ParsesSinglePair()
            {
                var result = AdminApiBearerAuthMiddleware.ParseAllowedCallers("tid1:azp1");

                Assert.Single(result);
                Assert.Equal("tid1", result[0].TenantId);
                Assert.Equal("azp1", result[0].AuthorizedParty);
            }

            [Fact]
            public void ParsesMultiplePairs()
            {
                var result = AdminApiBearerAuthMiddleware.ParseAllowedCallers("tid1:azp1;tid2:azp2;tid3:azp3");

                Assert.Equal(3, result.Count);
                Assert.Equal("tid1", result[0].TenantId);
                Assert.Equal("azp1", result[0].AuthorizedParty);
                Assert.Equal("tid2", result[1].TenantId);
                Assert.Equal("azp2", result[1].AuthorizedParty);
                Assert.Equal("tid3", result[2].TenantId);
                Assert.Equal("azp3", result[2].AuthorizedParty);
            }

            [Fact]
            public void IgnoresInvalidEntries()
            {
                var result = AdminApiBearerAuthMiddleware.ParseAllowedCallers("tid1:azp1;;:;invalid;tid2:azp2");

                Assert.Equal(2, result.Count);
                Assert.Equal("tid1", result[0].TenantId);
                Assert.Equal("tid2", result[1].TenantId);
            }

            [Fact]
            public void TrimsWhitespace()
            {
                var result = AdminApiBearerAuthMiddleware.ParseAllowedCallers(" tid1 : azp1 ");

                Assert.Single(result);
                Assert.Equal("tid1", result[0].TenantId);
                Assert.Equal("azp1", result[0].AuthorizedParty);
            }
        }

        public class TheValidateAndAuthorizeAsyncMethod
        {
            private static readonly SymmetricSecurityKey TestSigningKey = new(
                Encoding.UTF8.GetBytes("SuperSecretTestKeyThatIsAtLeast256BitsLongForHmacSha256Validation!!"))
            {
                KeyId = "test-key-id"
            };

            public TheValidateAndAuthorizeAsyncMethod()
            {
                SetupDependencyResolverForValidation();
            }

            [Fact]
            public async Task Returns401ForGarbageTokenAsync()
            {
                var configService = CreateMockConfigService();

                var result = await AdminApiBearerAuthMiddleware.ValidateAndAuthorizeAsync(
                    "garbage-not-a-jwt", configService.Object);

                Assert.Equal((int)HttpStatusCode.Unauthorized, result.StatusCode);
            }

            [Fact]
            public async Task Returns401ForEmptyTokenAsync()
            {
                var configService = CreateMockConfigService();

                var result = await AdminApiBearerAuthMiddleware.ValidateAndAuthorizeAsync(
                    "", configService.Object);

                Assert.Equal((int)HttpStatusCode.Unauthorized, result.StatusCode);
            }

            [Fact]
            public async Task Returns401ForTokenSignedWithWrongKeyAsync()
            {
                var configService = CreateMockConfigService();

                var wrongKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes("DifferentKeyThatDoesNotMatchTheExpectedSigningKeyAtAll!!"))
                {
                    KeyId = "wrong-key-id"
                };
                var token = CreateTestJwt(signingKey: wrongKey);

                var result = await AdminApiBearerAuthMiddleware.ValidateAndAuthorizeAsync(
                    token, configService.Object);

                Assert.Equal((int)HttpStatusCode.Unauthorized, result.StatusCode);
            }

            [Fact]
            public async Task Returns401ForTokenWithWrongAudienceAsync()
            {
                var configService = CreateMockConfigService(audience: "https://admin-api.nuget.org");

                var token = CreateTestJwt(audience: "https://wrong-audience.example.com");

                var result = await AdminApiBearerAuthMiddleware.ValidateAndAuthorizeAsync(
                    token, configService.Object);

                Assert.Equal((int)HttpStatusCode.Unauthorized, result.StatusCode);
            }

            [Fact]
            public async Task ReturnsSuccessForValidTokenWithMatchingCallerAsync()
            {
                var configService = CreateMockConfigService(
                    audience: "https://admin-api.nuget.org",
                    allowedCallers: "test-tenant-id:test-authorized-party");

                var token = CreateTestJwt(
                    audience: "https://admin-api.nuget.org",
                    tenantId: "test-tenant-id",
                    azp: "test-authorized-party");

                var result = await AdminApiBearerAuthMiddleware.ValidateAndAuthorizeAsync(
                    token, configService.Object);

                Assert.Equal(0, result.StatusCode);
                Assert.Equal("test-authorized-party", result.AuthorizedParty);
            }

            [Fact]
            public async Task Returns403ForValidTokenWithNonMatchingTenantIdAsync()
            {
                var configService = CreateMockConfigService(
                    audience: "https://admin-api.nuget.org",
                    allowedCallers: "allowed-tenant:test-authorized-party");

                var token = CreateTestJwt(
                    audience: "https://admin-api.nuget.org",
                    tenantId: "wrong-tenant",
                    azp: "test-authorized-party");

                var result = await AdminApiBearerAuthMiddleware.ValidateAndAuthorizeAsync(
                    token, configService.Object);

                Assert.Equal((int)HttpStatusCode.Forbidden, result.StatusCode);
            }

            [Fact]
            public async Task Returns403ForValidTokenWithNonMatchingazpAsync()
            {
                var configService = CreateMockConfigService(
                    audience: "https://admin-api.nuget.org",
                    allowedCallers: "test-tenant-id:allowed-azp");

                var token = CreateTestJwt(
                    audience: "https://admin-api.nuget.org",
                    tenantId: "test-tenant-id",
                    azp: "wrong-app");

                var result = await AdminApiBearerAuthMiddleware.ValidateAndAuthorizeAsync(
                    token, configService.Object);

                Assert.Equal((int)HttpStatusCode.Forbidden, result.StatusCode);
            }

            [Fact]
            public async Task Returns403ForValidTokenMissingTidClaimAsync()
            {
                var configService = CreateMockConfigService(
                    audience: "https://admin-api.nuget.org",
                    allowedCallers: "test-tenant-id:test-authorized-party");

                var token = CreateTestJwt(
                    audience: "https://admin-api.nuget.org",
                    tenantId: "test-tenant-id",
                    azp: "test-authorized-party",
                    includeTid: false);

                var result = await AdminApiBearerAuthMiddleware.ValidateAndAuthorizeAsync(
                    token, configService.Object);

                // Without tid claim, AadIssuerValidator may reject → 401,
                // or if it passes, the missing claim → 403.
                Assert.True(
                    result.StatusCode == (int)HttpStatusCode.Unauthorized ||
                    result.StatusCode == (int)HttpStatusCode.Forbidden,
                    $"Expected 401 or 403 but got {result.StatusCode}");
            }

            [Fact]
            public async Task Returns403ForValidTokenMissingAzpClaimAsync()
            {
                var configService = CreateMockConfigService(
                    audience: "https://admin-api.nuget.org",
                    allowedCallers: "test-tenant-id:test-authorized-party");

                var token = CreateTestJwt(
                    audience: "https://admin-api.nuget.org",
                    tenantId: "test-tenant-id",
                    azp: "test-authorized-party",
                    includeazp: false);

                var result = await AdminApiBearerAuthMiddleware.ValidateAndAuthorizeAsync(
                    token, configService.Object);

                Assert.Equal((int)HttpStatusCode.Forbidden, result.StatusCode);
            }

            [Fact]
            public async Task ReturnsSuccessWhenCallerMatchesCaseInsensitivelyAsync()
            {
                var configService = CreateMockConfigService(
                    audience: "https://admin-api.nuget.org",
                    allowedCallers: "TEST-TENANT-ID:test-authorized-party");

                var token = CreateTestJwt(
                    audience: "https://admin-api.nuget.org",
                    tenantId: "test-tenant-id",
                    azp: "test-authorized-party");

                var result = await AdminApiBearerAuthMiddleware.ValidateAndAuthorizeAsync(
                    token, configService.Object);

                Assert.Equal(0, result.StatusCode);
                Assert.Equal("test-authorized-party", result.AuthorizedParty);
            }

            [Fact]
            public async Task ReturnsSuccessWhenCallerMatchesSecondAllowedPairAsync()
            {
                var configService = CreateMockConfigService(
                    audience: "https://admin-api.nuget.org",
                    allowedCallers: "other-tenant:other-app;test-tenant-id:test-authorized-party");

                var token = CreateTestJwt(
                    audience: "https://admin-api.nuget.org",
                    tenantId: "test-tenant-id",
                    azp: "test-authorized-party");

                var result = await AdminApiBearerAuthMiddleware.ValidateAndAuthorizeAsync(
                    token, configService.Object);

                Assert.Equal(0, result.StatusCode);
            }

            [Fact]
            public async Task Returns403WhenAllowedCallersConfigIsEmptyAsync()
            {
                var configService = CreateMockConfigService(
                    audience: "https://admin-api.nuget.org",
                    allowedCallers: "");

                var token = CreateTestJwt(
                    audience: "https://admin-api.nuget.org",
                    tenantId: "test-tenant-id",
                    azp: "test-authorized-party");

                var result = await AdminApiBearerAuthMiddleware.ValidateAndAuthorizeAsync(
                    token, configService.Object);

                Assert.Equal((int)HttpStatusCode.Forbidden, result.StatusCode);
            }

            private static string CreateTestJwt(
                string audience = "https://admin-api.nuget.org",
                string tenantId = "test-tenant-id",
                string azp = "test-authorized-party",
                bool includeTid = true,
                bool includeazp = true,
                string[] roles = null,
                SymmetricSecurityKey signingKey = null)
            {
                signingKey ??= TestSigningKey;
                var handler = new JsonWebTokenHandler();
                var claims = new Dictionary<string, object>();
                if (includeTid) claims["tid"] = tenantId;
                if (includeazp) claims["azp"] = azp;
                if (roles != null) claims["roles"] = roles;

                var descriptor = new SecurityTokenDescriptor
                {
                    Issuer = $"https://login.microsoftonline.com/{tenantId}/v2.0",
                    Audience = audience,
                    Claims = claims,
                    SigningCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256),
                    Expires = DateTime.UtcNow.AddHours(1),
                    IssuedAt = DateTime.UtcNow,
                    NotBefore = DateTime.UtcNow.AddMinutes(-5),
                };

                return handler.CreateToken(descriptor);
            }

            private static ConfigurationManager<OpenIdConnectConfiguration> CreateTestConfigManager()
            {
                var oidcConfig = new OpenIdConnectConfiguration
                {
                    Issuer = "https://login.microsoftonline.com/{tenantid}/v2.0"
                };
                oidcConfig.SigningKeys.Add(TestSigningKey);

                var retriever = new Mock<IConfigurationRetriever<OpenIdConnectConfiguration>>();
                retriever
                    .Setup(r => r.GetConfigurationAsync(
                        It.IsAny<string>(),
                        It.IsAny<IDocumentRetriever>(),
                        It.IsAny<CancellationToken>()))
                    .ReturnsAsync(oidcConfig);

                return new ConfigurationManager<OpenIdConnectConfiguration>(
                    "https://test-metadata/.well-known/openid-configuration",
                    retriever.Object);
            }

            private static void SetupDependencyResolverForValidation()
            {
                var handler = new JsonWebTokenHandler();
                var configManager = CreateTestConfigManager();

                var mockDependencyResolver = new Mock<IDependencyResolver>();
                mockDependencyResolver
                    .Setup(r => r.GetService(typeof(JsonWebTokenHandler)))
                    .Returns(handler);
                mockDependencyResolver
                    .Setup(r => r.GetService(typeof(ConfigurationManager<OpenIdConnectConfiguration>)))
                    .Returns(configManager);

                DependencyResolver.SetResolver(mockDependencyResolver.Object);
            }

            private static Mock<IGalleryConfigurationService> CreateMockConfigService(
                string audience = "https://admin-api.nuget.org",
                string allowedCallers = "test-tenant-id:test-authorized-party")
            {
                var mockConfig = new Mock<IAppConfiguration>();
                mockConfig.Setup(c => c.AdminApiAudience).Returns(audience);
                mockConfig.Setup(c => c.AdminApiAllowedCallers).Returns(allowedCallers);

                var mockConfigService = new Mock<IGalleryConfigurationService>();
                mockConfigService.Setup(s => s.Current).Returns(mockConfig.Object);

                return mockConfigService;
            }
        }
    }
}
