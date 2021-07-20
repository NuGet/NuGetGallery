// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Security.Claims;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using Moq;
using NuGet.Services.Entities;
using NuGetGallery.Authentication;
using NuGetGallery.Configuration;
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

            [Theory]
            [InlineData(-1, "Your API key has expired. Visit https://www.nuget.org/account/apikeys to generate a new API key.")]
            [InlineData(10, "Your API key expires in 0 days. Visit https://www.nuget.org/account/apikeys to regenerate your API key.")]
            [InlineData(30, "Your API key expires in 1 day. Visit https://www.nuget.org/account/apikeys to regenerate your API key.")]
            [InlineData(50, "Your API key expires in 2 days. Visit https://www.nuget.org/account/apikeys to regenerate your API key.")]
            public void AddsExpirationWarnings(int expirationHours, string expectedWarning)
            {
                // Create an API key. We set the expiration using the property as the constructor does not
                // allow creating already expired keys.
                var apiKey = TestCredentialHelper.CreateV4ApiKey(expiration: null, out _);
                apiKey.Expires = DateTime.UtcNow.AddHours(expirationHours);

                var context = BuildAuthorizationContext(authenticated: true, credential: apiKey).Object;
                var attribute = new ApiAuthorizeAttribute();

                // Act
                attribute.OnAuthorization(context);

                var owinContext = context.HttpContext.GetOwinContext();

                // Assert
                Assert.Equal(200, owinContext.Response.StatusCode);
                Assert.True(context.HttpContext.Response.Headers.AllKeys.Contains("X-NuGet-Warning"));
                Assert.Equal(expectedWarning, context.HttpContext.Response.Headers["X-NuGet-Warning"]);
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

            private Mock<AuthorizationContext> BuildAuthorizationContext(
                bool authenticated = true,
                Credential credential = null)
            {
                var configs = new Mock<IGalleryConfigurationService>();
                configs.SetupGet(c => c.Current.RequireSSL).Returns(true);
                configs.SetupGet(c => c.Current.WarnAboutExpirationInDaysForApiKeyV1).Returns(10);
                configs.Setup(c => c.GetSiteRoot(true)).Returns("https://www.nuget.org");

                UrlHelperExtensions.SetConfigurationService(configs.Object);

                credential = credential ?? TestCredentialHelper.CreateV4ApiKey(expiration: null, plaintextApiKey: out string plaintextApiKey);

                var user = new User();
                user.Credentials.Add(credential);

                var mockHttpContext = new Mock<HttpContextBase>();
                mockHttpContext.SetupGet(c => c.Items).Returns(new Dictionary<object, object>
                {
                    { "owin.Environment", new Dictionary<string, object>() }
                });

                var mockIdentity = new Mock<ClaimsIdentity>();

                mockIdentity.SetupGet(i => i.Claims).Returns(new List<Claim>() { new Claim(NuGetClaims.ApiKey, user.Credentials.First().Value) });
                mockIdentity.SetupGet(i => i.IsAuthenticated).Returns(authenticated);
                mockIdentity.SetupGet(i => i.AuthenticationType).Returns(AuthenticationTypes.ApiKey);

                mockHttpContext.SetupGet(c => c.User.Identity).Returns(mockIdentity.Object);
                mockHttpContext.SetupGet(c => c.Response.Cache).Returns(Mock.Of<HttpCachePolicyBase>());
                mockHttpContext.SetupGet(c => c.Response.Headers).Returns(new NameValueCollection());
                mockHttpContext.SetupGet(c => c.Request.IsSecureConnection).Returns(true);

                var mockActionDescriptor = new Mock<ActionDescriptor>();
                mockActionDescriptor.Setup(c => c.ControllerDescriptor).Returns(new Mock<ControllerDescriptor>().Object);

                var mockController = new Mock<AppController>();
                mockController.Setup(c => c.GetCurrentUser()).Returns(user);

                var controller = mockController.Object;
                controller.NuGetContext.Config = configs.Object;
                TestUtility.SetupUrlHelperForUrlGeneration(controller);

                var mockAuthContext = new Mock<AuthorizationContext>(MockBehavior.Strict);
                mockAuthContext.SetupGet(c => c.HttpContext).Returns(mockHttpContext.Object);
                mockAuthContext.SetupGet(c => c.ActionDescriptor).Returns(mockActionDescriptor.Object);
                mockAuthContext.SetupGet(c => c.Controller).Returns(controller);
                mockAuthContext.SetupGet(c => c.RouteData).Returns(Mock.Of<RouteData>());

                return mockAuthContext;
            }
        }
    }
}