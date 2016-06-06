// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Web;
using System.Web.Mvc;
using Moq;
using NuGetGallery.Authentication;
using Xunit;

namespace NuGetGallery.Filters
{
    public class ApiAuthorizeAttributeFacts
    {
        [Fact]
        public void ApiAuthorizeAttributeReturns401()
        {
            var httpContext = new Mock<HttpContextBase>();
            httpContext.SetupGet(c => c.Items).Returns(new Dictionary<object, object> { { "owin.Environment", new Dictionary<string, object>() } });
            httpContext.SetupGet(c => c.User.Identity.IsAuthenticated).Returns(false);
            var actionDescriptor = new Mock<ActionDescriptor>();
            var controllerDescriptor = new Mock<ControllerDescriptor>();
            actionDescriptor.Setup(c => c.ControllerDescriptor).Returns(controllerDescriptor.Object);
            var mockAuthContext = new Mock<AuthorizationContext>(MockBehavior.Strict);
            mockAuthContext.SetupGet(c => c.ActionDescriptor).Returns(actionDescriptor.Object);
            mockAuthContext.SetupGet(c => c.HttpContext).Returns(httpContext.Object);
            mockAuthContext.SetupGet(c => c.Controller).Returns((Controller)null);
            var context = mockAuthContext.Object;
            var attribute = new ApiAuthorizeAttribute();

            // Act
            attribute.OnAuthorization(context);

            var owinContext = context.HttpContext.GetOwinContext();

            // Assert
            Assert.IsType<HttpUnauthorizedResult>(context.Result);
            Assert.Equal(401, owinContext.Response.StatusCode);
            Assert.Equal(AuthenticationTypes.ApiKey, owinContext.Authentication.AuthenticationResponseChallenge.AuthenticationTypes[0]);
        }
    }
}