// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Security.Claims;
using System.Web;
using System.Web.Mvc;
using Moq;
using NuGetGallery.Configuration;
using Xunit;
using AuthorizationContext = System.Web.Mvc.AuthorizationContext;

namespace NuGetGallery.Filters
{
    public class ElevatedAdminActionAttributeFacts
    {
        public class TheOnAuthorizationMethod
        {
            [Fact]
            public void DeniesAuthenticatedUserWithoutElevatedAdminRole()
            {
                // Arrange
                SetupConfigService(adminPanelEnabled: true);
                var context = BuildAuthorizationContext(roles: new string[0]).Object;
                var attribute = new ElevatedAdminActionAttribute();

                // Act
                attribute.OnAuthorization(context);

                // Assert
                Assert.IsType<HttpUnauthorizedResult>(context.Result);
            }

            [Fact]
            public void AllowsAuthenticatedUserWithElevatedAdminRole()
            {
                // Arrange
                SetupConfigService(adminPanelEnabled: true);
                var context = BuildAuthorizationContext(roles: new[] { CoreConstants.ElevatedAdminRoleName }).Object;
                var attribute = new ElevatedAdminActionAttribute();

                // Act
                attribute.OnAuthorization(context);

                // Assert
                Assert.Null(context.Result);
            }

            private static void SetupConfigService(bool adminPanelEnabled)
            {
                var mockConfig = new Mock<IAppConfiguration>();
                mockConfig.Setup(c => c.AdminPanelEnabled).Returns(adminPanelEnabled);

                var mockConfigService = new Mock<IGalleryConfigurationService>();
                mockConfigService.Setup(s => s.Current).Returns(mockConfig.Object);

                var mockDependencyResolver = new Mock<IDependencyResolver>();
                mockDependencyResolver
                    .Setup(r => r.GetService(typeof(IGalleryConfigurationService)))
                    .Returns(mockConfigService.Object);

                DependencyResolver.SetResolver(mockDependencyResolver.Object);
            }

            private static Mock<AuthorizationContext> BuildAuthorizationContext(string[] roles)
            {
                var claims = new List<Claim>();
                foreach (var role in roles)
                {
                    claims.Add(new Claim(ClaimTypes.Role, role));
                }

                var identity = new ClaimsIdentity(claims, authenticationType: "Test");
                var principal = new ClaimsPrincipal(identity);

                var mockHttpContext = new Mock<HttpContextBase>();
                mockHttpContext.SetupGet(c => c.Items).Returns(new Dictionary<object, object> {
                    { "owin.Environment", new Dictionary<string, object>() }
                });
                mockHttpContext.SetupGet(c => c.User).Returns(principal);
                mockHttpContext.SetupGet(c => c.Response.Cache).Returns(new Mock<HttpCachePolicyBase>().Object);

                var mockController = new Mock<AppController>();

                var mockActionDescriptor = new Mock<ActionDescriptor>();
                mockActionDescriptor.Setup(c => c.ControllerDescriptor).Returns(new Mock<ControllerDescriptor>().Object);

                var mockAuthContext = new Mock<AuthorizationContext>(MockBehavior.Strict);
                mockAuthContext.SetupGet(c => c.HttpContext).Returns(mockHttpContext.Object);
                mockAuthContext.SetupGet(c => c.ActionDescriptor).Returns(mockActionDescriptor.Object);
                mockAuthContext.SetupGet(c => c.Controller).Returns(mockController.Object);
                mockAuthContext.SetupGet(c => c.RouteData).Returns(new Mock<System.Web.Routing.RouteData>().Object);

                mockAuthContext.Object.Result = null;

                return mockAuthContext;
            }
        }
    }
}
