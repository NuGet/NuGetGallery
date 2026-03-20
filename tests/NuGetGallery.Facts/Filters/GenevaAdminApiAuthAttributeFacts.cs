// Copyright(c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using Moq;
using NuGetGallery.Configuration;
using Xunit;

namespace NuGetGallery.Filters
{
	public class GenevaAdminApiAuthAttributeFacts
	{
		public class TheOnAuthorizationMethod
		{
			[Fact]
			public void Returns404WhenAdminPanelDisabled()
			{
				// Arrange
				var context = BuildAuthorizationContext(headers: []);
				SetupConfigService(adminPanelEnabled: false, genevaAdminApiEnabled: true);

				var attribute = new AdminApiAuthAttribute();

				// Act
				attribute.OnAuthorization(context.Object);

				// Assert
				var result = context.Object.Result as HttpStatusCodeResult;
				Assert.NotNull(result);
				Assert.Equal((int)HttpStatusCode.NotFound, result.StatusCode);
			}

			[Fact]
			public void Returns404WhenGenevaAdminApiDisabled()
			{
				// Arrange
				var context = BuildAuthorizationContext(headers: []);
				SetupConfigService(adminPanelEnabled: true, genevaAdminApiEnabled: false);

				var attribute = new AdminApiAuthAttribute();

				// Act
				attribute.OnAuthorization(context.Object);

				// Assert
				var result = context.Object.Result as HttpStatusCodeResult;
				Assert.NotNull(result);
				Assert.Equal((int)HttpStatusCode.NotFound, result.StatusCode);
			}

			[Fact]
			public void Returns404WhenBothDisabled()
			{
				// Arrange
				var context = BuildAuthorizationContext(headers: []);
				SetupConfigService(adminPanelEnabled: false, genevaAdminApiEnabled: false);

				var attribute = new AdminApiAuthAttribute();

				// Act
				attribute.OnAuthorization(context.Object);

				// Assert
				var result = context.Object.Result as HttpStatusCodeResult;
				Assert.NotNull(result);
				Assert.Equal((int)HttpStatusCode.NotFound, result.StatusCode);
			}

			[Fact]
			public void Returns401WhenNoAuthorizationHeader()
			{
				// Arrange
				var context = BuildAuthorizationContext(headers: []);
				SetupConfigService(adminPanelEnabled: true, genevaAdminApiEnabled: true);

				var attribute = new AdminApiAuthAttribute();

				// Act
				attribute.OnAuthorization(context.Object);

				// Assert
				var result = context.Object.Result as HttpStatusCodeResult;
				Assert.NotNull(result);
				Assert.Equal((int)HttpStatusCode.Unauthorized, result.StatusCode);
			}

			[Theory]
			[InlineData("")]
			[InlineData("Basic abc123")]
			[InlineData("NotBearer xyz")]
			public void Returns401WhenAuthorizationHeaderIsNotBearer(string authHeader)
			{
				// Arrange
				var headers = new NameValueCollection { { "Authorization", authHeader } };
				var context = BuildAuthorizationContext(headers: headers);
				SetupConfigService(adminPanelEnabled: true, genevaAdminApiEnabled: true);

				var attribute = new AdminApiAuthAttribute();

				// Act
				attribute.OnAuthorization(context.Object);

				// Assert
				var result = context.Object.Result as HttpStatusCodeResult;
				Assert.NotNull(result);
				Assert.Equal((int)HttpStatusCode.Unauthorized, result.StatusCode);
			}

			private static void SetupConfigService(
				bool adminPanelEnabled,
				bool genevaAdminApiEnabled,
				string audience = "https://admin-api.nuget.org",
				string allowedCallers = "tenant1:app1")
			{
				var mockConfig = new Mock<IAppConfiguration>();
				mockConfig.Setup(c => c.AdminPanelEnabled).Returns(adminPanelEnabled);
				mockConfig.Setup(c => c.GenevaAdminApiEnabled).Returns(genevaAdminApiEnabled);
				mockConfig.Setup(c => c.GenevaAdminApiAudience).Returns(audience);
				mockConfig.Setup(c => c.GenevaAdminApiAllowedCallers).Returns(allowedCallers);

				var mockConfigService = new Mock<IGalleryConfigurationService>();
				mockConfigService.Setup(s => s.Current).Returns(mockConfig.Object);

				var mockDependencyResolver = new Mock<IDependencyResolver>();
				mockDependencyResolver
					.Setup(r => r.GetService(typeof(IGalleryConfigurationService)))
					.Returns(mockConfigService.Object);

				DependencyResolver.SetResolver(mockDependencyResolver.Object);
			}

			private static Mock<AuthorizationContext> BuildAuthorizationContext(NameValueCollection headers)
			{
				var mockController = new Mock<AppController>();

				var mockRequest = new Mock<HttpRequestBase>();
				mockRequest.Setup(r => r.Headers).Returns(headers);

				var mockHttpContext = new Mock<HttpContextBase>();
				mockHttpContext.Setup(c => c.Request).Returns(mockRequest.Object);
				mockHttpContext.SetupGet(c => c.Items).Returns(new Dictionary<object, object>
				{
					{ "owin.Environment", new Dictionary<string, object>() }
				});
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
				var mockRequest = new Mock<HttpRequestBase>();
				mockRequest.Setup(r => r.Headers).Returns([]);

				var result = AdminApiAuthAttribute.ExtractBearerToken(mockRequest.Object);

				Assert.Null(result);
			}

			[Fact]
			public void ReturnsTokenWhenBearerPrefix()
			{
				var headers = new NameValueCollection { { "Authorization", "Bearer mytoken123" } };
				var mockRequest = new Mock<HttpRequestBase>();
				mockRequest.Setup(r => r.Headers).Returns(headers);

				var result = AdminApiAuthAttribute.ExtractBearerToken(mockRequest.Object);

				Assert.Equal("mytoken123", result);
			}

			[Fact]
			public void ReturnsNullWhenNonBearerScheme()
			{
				var headers = new NameValueCollection { { "Authorization", "Basic abc123" } };
				var mockRequest = new Mock<HttpRequestBase>();
				mockRequest.Setup(r => r.Headers).Returns(headers);

				var result = AdminApiAuthAttribute.ExtractBearerToken(mockRequest.Object);

				Assert.Null(result);
			}

			[Fact]
			public void IsCaseInsensitive()
			{
				var headers = new NameValueCollection { { "Authorization", "bearer mytoken" } };
				var mockRequest = new Mock<HttpRequestBase>();
				mockRequest.Setup(r => r.Headers).Returns(headers);

				var result = AdminApiAuthAttribute.ExtractBearerToken(mockRequest.Object);

				Assert.Equal("mytoken", result);
			}
		}

		public class TheParseAllowedCallersMethod
		{
			[Fact]
			public void ReturnsEmptyForNull()
			{
				var result = AdminApiAuthAttribute.ParseAllowedCallers(null);

				Assert.Empty(result);
			}

			[Fact]
			public void ReturnsEmptyForEmptyString()
			{
				var result = AdminApiAuthAttribute.ParseAllowedCallers("");

				Assert.Empty(result);
			}

			[Fact]
			public void ParsesSinglePair()
			{
				var result = AdminApiAuthAttribute.ParseAllowedCallers("tid1:appid1");

				Assert.Single(result);
				Assert.Equal("tid1", result[0].TenantId);
				Assert.Equal("appid1", result[0].AppId);
			}

			[Fact]
			public void ParsesMultiplePairs()
			{
				var result = AdminApiAuthAttribute.ParseAllowedCallers("tid1:appid1;tid2:appid2;tid3:appid3");

				Assert.Equal(3, result.Count);
				Assert.Equal("tid1", result[0].TenantId);
				Assert.Equal("appid1", result[0].AppId);
				Assert.Equal("tid2", result[1].TenantId);
				Assert.Equal("appid2", result[1].AppId);
				Assert.Equal("tid3", result[2].TenantId);
				Assert.Equal("appid3", result[2].AppId);
			}

			[Fact]
			public void IgnoresInvalidEntries()
			{
				var result = AdminApiAuthAttribute.ParseAllowedCallers("tid1:appid1;;:;invalid;tid2:appid2");

				Assert.Equal(2, result.Count);
				Assert.Equal("tid1", result[0].TenantId);
				Assert.Equal("tid2", result[1].TenantId);
			}

			[Fact]
			public void TrimsWhitespace()
			{
				var result = AdminApiAuthAttribute.ParseAllowedCallers(" tid1 : appid1 ");

				Assert.Single(result);
				Assert.Equal("tid1", result[0].TenantId);
				Assert.Equal("appid1", result[0].AppId);
			}
		}
	}
}
