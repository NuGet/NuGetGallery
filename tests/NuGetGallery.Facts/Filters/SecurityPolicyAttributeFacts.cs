// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Web;
using System.Web.Mvc;
using Moq;
using NuGetGallery.Security;
using Xunit;

namespace NuGetGallery.Filters
{
    public class SecurityPolicyAttributeFacts
    {
        [Fact]
        public void OnAuthorizationReturnsNullResultOnSecurityPolicyResultSuccess()
        {
            // Arrange
            var mockService = new Mock<ISecurityPolicyService>();
            mockService.Setup(s => s.Evaluate(It.IsAny<SecurityPolicyAction>(), It.IsAny<HttpContextBase>()))
                .Returns(SecurityPolicyResult.SuccessResult).Verifiable();
            
            var mockContext = BuildAuthorizationContext(mockService);
            var context = mockContext.Object;

            // Act
            new SecurityPolicyAttribute(SecurityPolicyAction.PackagePush).OnAuthorization(context);
            var owinContext = context.HttpContext.GetOwinContext();

            // Assert
            mockService.Verify();
            Assert.Null(context.Result);
            Assert.Equal(200, owinContext.Response.StatusCode);
        }

        [Fact]
        public void OnAuthorizationReturns400ResultOnSecurityPolicyResultFailure()
        {
            // Arrange
            var mockService = new Mock<ISecurityPolicyService>();
            mockService.Setup(s => s.Evaluate(It.IsAny<SecurityPolicyAction>(), It.IsAny<HttpContextBase>()))
                .Returns(new SecurityPolicyResult(false, "A")).Verifiable();

            var mockContext = BuildAuthorizationContext(mockService);
            var context = mockContext.Object;

            // Act
            new SecurityPolicyAttribute(SecurityPolicyAction.PackagePush).OnAuthorization(context);
            var owinContext = context.HttpContext.GetOwinContext();

            // Assert
            mockService.Verify();
            Assert.IsType<HttpStatusCodeWithBodyResult>(context.Result);
            Assert.Equal(400, owinContext.Response.StatusCode);
        }

        private Mock<AuthorizationContext> BuildAuthorizationContext(Mock<ISecurityPolicyService> mockService)
        {
            var mockController = new Mock<AppController>();
            mockController.Setup(x => x.GetService<ISecurityPolicyService>()).Returns(mockService.Object);

            var mockHttpContext = new Mock<HttpContextBase>();
            mockHttpContext.SetupGet(c => c.Items).Returns(new Dictionary<object, object> {
                { "owin.Environment", new Dictionary<string, object>() }
            });
            mockHttpContext.SetupGet(c => c.User.Identity.IsAuthenticated).Returns(true);
            mockHttpContext.SetupGet(c => c.Response.Cache).Returns(new Mock<HttpCachePolicyBase>().Object);

            var mockActionDescriptor = new Mock<ActionDescriptor>();
            mockActionDescriptor.Setup(c => c.ControllerDescriptor).Returns(new Mock<ControllerDescriptor>().Object);

            var mockAuthContext = new Mock<AuthorizationContext>(MockBehavior.Strict);
            mockAuthContext.SetupGet(c => c.HttpContext).Returns(mockHttpContext.Object);
            mockAuthContext.SetupGet(c => c.ActionDescriptor).Returns(mockActionDescriptor.Object);
            mockAuthContext.SetupGet(c => c.Controller).Returns(mockController.Object);
            mockAuthContext.SetupGet(c => c.RouteData).Returns(new Mock<System.Web.Routing.RouteData>().Object);

            return mockAuthContext;
        }
    }
}