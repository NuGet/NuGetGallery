// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Moq;
using NuGetGallery.Authentication;
using NuGetGallery.Security;
using Xunit;

namespace NuGetGallery.Filters
{
    public class ApiAuthorizeAttributeFacts
    {
        [Fact]
        public void OnAuthorization_Returns401ForUnauthenticatedUser()
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