﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Web;
using System.Web.Mvc;
using Moq;
using NuGetGallery.Authentication;
using Xunit;
using NuGetGallery.Security;

namespace NuGetGallery.Filters
{
    public class ApiAuthorizeAttributeFacts
    {
        [Fact]
        public void ApiAuthorizeAttributeReturns401_UnauthenticatedUser()
        {
            var context = BuildAuthorizationContext(authenticated: false).Object;
            var attribute = new ApiAuthorizeAttribute();

            // Act
            attribute.OnAuthorization(context);

            var owinContext = context.HttpContext.GetOwinContext();

            // Assert
            Assert.IsType<HttpUnauthorizedResult>(context.Result);
            Assert.Equal(401, owinContext.Response.StatusCode);
            Assert.Equal(AuthenticationTypes.ApiKey, owinContext.Authentication.AuthenticationResponseChallenge.AuthenticationTypes[0]);
        }

        [Fact]
        public void ApiAuthorizeAttributeReturns200_SecurityPolicyResultSuccess()
        {
            // Arrange
            var mockService = new Mock<ISecurityPolicyService>();
            mockService.Setup(s => s.Evaluate(It.IsAny<SecurityPolicyAction>(), It.IsAny<HttpContextBase>()))
                .Returns(SecurityPolicyResult.SuccessResult).Verifiable();

            var mockContext = BuildAuthorizationContext(policyService: mockService);
            var context = mockContext.Object;

            // Act
            new ApiAuthorizeAttribute(SecurityPolicyAction.PackagePush).OnAuthorization(context);
            var owinContext = context.HttpContext.GetOwinContext();

            // Assert
            mockService.Verify();
            Assert.Null(context.Result);
            Assert.Equal(200, owinContext.Response.StatusCode);
        }

        [Fact]
        public void ApiAuthorizeAttributeReturns400_SecurityPolicyResultFailure()
        {
            // Arrange
            var mockService = new Mock<ISecurityPolicyService>();
            mockService.Setup(s => s.Evaluate(It.IsAny<SecurityPolicyAction>(), It.IsAny<HttpContextBase>()))
                .Returns(SecurityPolicyResult.CreateErrorResult("A")).Verifiable();

            var mockContext = BuildAuthorizationContext(policyService: mockService);
            var context = mockContext.Object;

            // Act
            new ApiAuthorizeAttribute(SecurityPolicyAction.PackagePush).OnAuthorization(context);
            var owinContext = context.HttpContext.GetOwinContext();

            // Assert
            mockService.Verify();
            Assert.IsType<HttpStatusCodeWithBodyResult>(context.Result);
            Assert.Equal(400, owinContext.Response.StatusCode);
        }

        private Mock<AuthorizationContext> BuildAuthorizationContext(bool authenticated = true, Mock<ISecurityPolicyService> policyService = null)
        {
            var mockController = new Mock<AppController>();
            mockController.Setup(x => x.GetService<ISecurityPolicyService>()).Returns(policyService?.Object);

            var mockHttpContext = new Mock<HttpContextBase>();
            mockHttpContext.SetupGet(c => c.Items).Returns(new Dictionary<object, object> {
                { "owin.Environment", new Dictionary<string, object>() }
            });
            mockHttpContext.SetupGet(c => c.User.Identity.IsAuthenticated).Returns(authenticated);
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