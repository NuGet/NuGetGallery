// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Web;
using System.Web.Mvc;
using Moq;
using NuGet.Services.Entities;
using NuGetGallery.Authentication;
using Xunit;
using AuthenticationTypes = NuGetGallery.Authentication.AuthenticationTypes;
using AuthorizationContext = System.Web.Mvc.AuthorizationContext;

namespace NuGetGallery.Filters
{
    public class ApiAuthorizeAttributeFacts
    {
        public class TheOnAuthorizationMethod
        {
            [Fact]
            public void Returns401ForUnauthenticatedUser()
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
            public void SucceedsForAuthenticatedUser()
            {
                var context = BuildAuthorizationContext(authenticated: true).Object;
                var attribute = new ApiAuthorizeAttribute();

                // Act
                attribute.OnAuthorization(context);

                var owinContext = context.HttpContext.GetOwinContext();

                // Assert
                Assert.Equal(200, owinContext.Response.StatusCode);
            }

            private Mock<AuthorizationContext> BuildAuthorizationContext(bool authenticated = true)
            {
                var mockController = new Mock<AppController>();
                var user = new User();
                user.Credentials.Add(TestCredentialHelper.CreateV4ApiKey(expiration: null, plaintextApiKey: out string plaintextApiKey));

                mockController.Setup(c => c.GetCurrentUser()).Returns(user);

                var mockHttpContext = new Mock<HttpContextBase>();
                mockHttpContext.SetupGet(c => c.Items).Returns(new Dictionary<object, object> {
                { "owin.Environment", new Dictionary<string, object>() }
            });

                var mockIdentity = new Mock<ClaimsIdentity>();

                mockIdentity.SetupGet(i => i.Claims).Returns(new List<Claim>() { new Claim(NuGetClaims.ApiKey, user.Credentials.First().Value) });
                mockIdentity.SetupGet(i => i.IsAuthenticated).Returns(authenticated);
                mockIdentity.SetupGet(i => i.AuthenticationType).Returns(AuthenticationTypes.ApiKey);

                mockHttpContext.SetupGet(c => c.User.Identity).Returns(mockIdentity.Object);
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
}