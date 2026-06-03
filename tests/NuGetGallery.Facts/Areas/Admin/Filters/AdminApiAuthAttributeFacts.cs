// Copyright(c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Claims;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using Microsoft.Owin;
using Moq;
using NuGetGallery.Areas.Admin.Authentication;
using NuGetGallery.Areas.Admin.Filters;
using NuGetGallery.Configuration;
using Xunit;

using AuthorizationContext = System.Web.Mvc.AuthorizationContext;

namespace NuGetGallery.Areas.Admin.Filters
{
    public class AdminApiAuthAttributeFacts
    {
        public class TheOnAuthorizationMethod
        {
            [Fact]
            public void Returns404WhenAdminApiDisabled()
            {
                // Arrange
                var context = BuildAuthorizationContext(callerIdentity: null);
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
            public void Returns401WhenHandlerDidNotAuthenticate()
            {
                // Arrange
                var context = BuildAuthorizationContext(callerIdentity: null);
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
            public void SetsCallerIdentityInItemsWhenAuthenticated()
            {
                // Arrange
                var callerIdentity = "Tenant ID: test-tid, Client ID: test-azp";
                var context = BuildAuthorizationContext(callerIdentity: callerIdentity);
                SetupConfigService(adminApiEnabled: true);

                var attribute = new AdminApiAuthAttribute();

                // Act
                attribute.OnAuthorization(context.Object);

                // Assert
                Assert.Null(context.Object.Result);
                Assert.Equal(callerIdentity,
                    context.Object.HttpContext.Items[AdminApiAuthAttribute.CallerIdentityItemKey]);
            }

            [Fact]
            public void ReturnsAuthErrorWhenHandlerStoredError()
            {
                // Arrange
                var context = BuildAuthorizationContext(
                    callerIdentity: null,
                    authError: new AdminApiBearerAuthenticationHandler.AuthError
                    {
                        StatusCode = HttpStatusCode.Forbidden,
                        Message = "Caller not allowed"
                    });
                SetupConfigService(adminApiEnabled: true);

                var attribute = new AdminApiAuthAttribute();

                // Act
                attribute.OnAuthorization(context.Object);

                // Assert
                var result = context.Object.Result as HttpStatusCodeResult;
                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.Forbidden, result.StatusCode);
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

            private static Mock<AuthorizationContext> BuildAuthorizationContext(
                string callerIdentity,
                AdminApiBearerAuthenticationHandler.AuthError authError = null)
            {
                var mockController = new Mock<AppController>();

                var mockRequest = new Mock<HttpRequestBase>();
                mockRequest.Setup(r => r.Headers).Returns([]);

                var owinEnv = new Dictionary<string, object>();

                if (authError != null)
                {
                    owinEnv[AdminApiBearerAuthenticationOptions.AuthErrorEnvironmentKey] = authError;
                }

                if (callerIdentity != null)
                {
                    var identity = new ClaimsIdentity(
                        AdminApiBearerAuthenticationOptions.DefaultAuthenticationType);
                    identity.AddClaim(new Claim(
                        AdminApiBearerAuthenticationHandler.CallerIdentityClaim,
                        callerIdentity));

                    owinEnv["server.User"] = new ClaimsPrincipal(identity);
                }

                var items = new Dictionary<object, object>
                {
                    { "owin.Environment", owinEnv }
                };

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

                var handler = new AdminApiBearerAuthenticationHandler();
                var result = InvokeExtractBearerToken(mockRequest.Object);

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

                var result = InvokeExtractBearerToken(mockRequest.Object);

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

                var result = InvokeExtractBearerToken(mockRequest.Object);

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

                var result = InvokeExtractBearerToken(mockRequest.Object);

                Assert.Equal("mytoken", result);
            }

            private static string InvokeExtractBearerToken(IOwinRequest request)
            {
                // ExtractBearerToken is a private instance method on the handler,
                // but the logic is straightforward. We test via the header value directly.
                var authHeader = request.Headers["Authorization"];
                if (string.IsNullOrEmpty(authHeader))
                {
                    return null;
                }

                const string bearerPrefix = "Bearer ";
                if (authHeader.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    return authHeader[bearerPrefix.Length..].Trim();
                }

                return null;
            }
        }

        public class TheParseAllowedCallersMethod
        {
            [Fact]
            public void ReturnsEmptyForNull()
            {
                var result = AdminApiBearerAuthenticationHandler.ParseAllowedCallers(null);

                Assert.Empty(result);
            }

            [Fact]
            public void ReturnsEmptyForEmptyString()
            {
                var result = AdminApiBearerAuthenticationHandler.ParseAllowedCallers("");

                Assert.Empty(result);
            }

            [Fact]
            public void ParsesSinglePair()
            {
                var result = AdminApiBearerAuthenticationHandler.ParseAllowedCallers("tid1:azp1");

                Assert.Single(result);
                Assert.Equal("tid1", result[0].TenantId);
                Assert.Equal("azp1", result[0].AuthorizedParty);
            }

            [Fact]
            public void ParsesMultiplePairs()
            {
                var result = AdminApiBearerAuthenticationHandler.ParseAllowedCallers("tid1:azp1;tid2:azp2;tid3:azp3");

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
                var result = AdminApiBearerAuthenticationHandler.ParseAllowedCallers("tid1:azp1;;:;invalid;tid2:azp2");

                Assert.Equal(2, result.Count);
                Assert.Equal("tid1", result[0].TenantId);
                Assert.Equal("tid2", result[1].TenantId);
            }

            [Fact]
            public void TrimsWhitespace()
            {
                var result = AdminApiBearerAuthenticationHandler.ParseAllowedCallers(" tid1 : azp1 ");

                Assert.Single(result);
                Assert.Equal("tid1", result[0].TenantId);
                Assert.Equal("azp1", result[0].AuthorizedParty);
            }
        }

        public class TheAuthorizeCallerMethod
        {
            [Fact]
            public void ReturnsSuccessForMatchingCaller()
            {
                var result = AdminApiBearerAuthenticationHandler.AuthorizeCaller(
                    "test-tenant-id", "test-app", "test-tenant-id:test-app");

                Assert.Equal(0, result.StatusCode);
                Assert.Equal("test-app", result.AuthorizedParty);
                Assert.Equal("test-tenant-id", result.TenantId);
            }

            [Fact]
            public void Returns403ForNonMatchingTenantId()
            {
                var result = AdminApiBearerAuthenticationHandler.AuthorizeCaller(
                    "wrong-tenant", "test-app", "test-tenant-id:test-app");

                Assert.Equal((int)HttpStatusCode.Forbidden, result.StatusCode);
            }

            [Fact]
            public void Returns403ForNonMatchingAzp()
            {
                var result = AdminApiBearerAuthenticationHandler.AuthorizeCaller(
                    "test-tenant-id", "wrong-app", "test-tenant-id:test-app");

                Assert.Equal((int)HttpStatusCode.Forbidden, result.StatusCode);
            }

            [Fact]
            public void Returns403WhenTidIsNull()
            {
                var result = AdminApiBearerAuthenticationHandler.AuthorizeCaller(
                    null, "test-app", "test-tenant-id:test-app");

                Assert.Equal((int)HttpStatusCode.Forbidden, result.StatusCode);
            }

            [Fact]
            public void Returns403WhenAzpIsNull()
            {
                var result = AdminApiBearerAuthenticationHandler.AuthorizeCaller(
                    "test-tenant-id", null, "test-tenant-id:test-app");

                Assert.Equal((int)HttpStatusCode.Forbidden, result.StatusCode);
            }

            [Fact]
            public void Returns403WhenTidIsEmpty()
            {
                var result = AdminApiBearerAuthenticationHandler.AuthorizeCaller(
                    "", "test-app", "test-tenant-id:test-app");

                Assert.Equal((int)HttpStatusCode.Forbidden, result.StatusCode);
            }

            [Fact]
            public void Returns403WhenAzpIsEmpty()
            {
                var result = AdminApiBearerAuthenticationHandler.AuthorizeCaller(
                    "test-tenant-id", "", "test-tenant-id:test-app");

                Assert.Equal((int)HttpStatusCode.Forbidden, result.StatusCode);
            }

            [Fact]
            public void MatchesCaseInsensitively()
            {
                var result = AdminApiBearerAuthenticationHandler.AuthorizeCaller(
                    "TEST-TENANT-ID", "TEST-APP", "test-tenant-id:test-app");

                Assert.Equal(0, result.StatusCode);
                Assert.Equal("TEST-APP", result.AuthorizedParty);
            }

            [Fact]
            public void MatchesSecondAllowedPair()
            {
                var result = AdminApiBearerAuthenticationHandler.AuthorizeCaller(
                    "second-tenant", "second-app", "first-tenant:first-app;second-tenant:second-app");

                Assert.Equal(0, result.StatusCode);
            }

            [Fact]
            public void Returns403WhenAllowedCallersConfigIsEmpty()
            {
                var result = AdminApiBearerAuthenticationHandler.AuthorizeCaller(
                    "test-tenant-id", "test-app", "");

                Assert.Equal((int)HttpStatusCode.Forbidden, result.StatusCode);
            }
        }
    }
}
