// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Web;
using System.Web.Mvc;
using Moq;
using NuGetGallery.Authentication;
using NuGetGallery.Framework;
using Xunit;

namespace NuGetGallery.Filters
{
    public class UiAuthorizeAttributeFacts
    {
        public class TheOnAuthorizationMethod
        {
            private static IEnumerable<string> AuthTypes = new[] 
            {
                AuthenticationTypes.LocalUser,
                AuthenticationTypes.ApiKey,
                AuthenticationTypes.External
            };

            public static IEnumerable<object[]> AllowsDiscontinuedPassword_Data
            {
                get
                {
                    yield return MemberDataHelper.AsData(false);
                    yield return MemberDataHelper.AsData(true);
                }
            }

            public static IEnumerable<object[]> FailsForUnauthenticatedUser_Data
            {
                get
                {
                    foreach (var allowsDiscontinuedPassword in new[] { false, true })
                    {
                        foreach (var authType in AuthTypes)
                        {
                            yield return MemberDataHelper.AsData(allowsDiscontinuedPassword, BuildClaimsIdentity(authType, authenticated: false, hasDiscontinuedPasswordClaim: false).Object);
                            yield return MemberDataHelper.AsData(allowsDiscontinuedPassword, BuildClaimsIdentity(authType, authenticated: false, hasDiscontinuedPasswordClaim: true).Object);
                        }
                    }
                }
            }

            [Theory]
            [MemberData(nameof(FailsForUnauthenticatedUser_Data))]
            public void FailsForUnauthenticatedUser(bool allowsDiscontinuedPassword, ClaimsIdentity identity)
            {
                var context = BuildAuthorizationContext(identity).Object;
                var attribute = new UiAuthorizeAttribute(allowsDiscontinuedPassword);

                // Act
                attribute.OnAuthorization(context);

                // Assert
                Assert.IsType<HttpUnauthorizedResult>(context.Result);
            }

            public static IEnumerable<object[]> SucceedsForAuthenticatedUserWithoutDiscontinuedPassword_Data => 
                MemberDataHelper
                    .Combine(
                        AllowsDiscontinuedPassword_Data, 
                        AuthTypes.Select(t => MemberDataHelper.AsData(t, false)))
                    .Concat(
                        AuthTypes.Select(t => MemberDataHelper.AsData(true, t, true)));

            [Theory]
            [MemberData(nameof(SucceedsForAuthenticatedUserWithoutDiscontinuedPassword_Data))]
            public void SucceedsForAuthenticatedUserWithoutDiscontinuedPassword(bool allowsDiscontinuedPassword, string authType, bool hasDiscontinuedPasswordClaim)
            {
                var context = BuildAuthorizationContext(BuildClaimsIdentity(authType, authenticated: true, hasDiscontinuedPasswordClaim: hasDiscontinuedPasswordClaim).Object).Object;
                var attribute = new UiAuthorizeAttribute(allowsDiscontinuedPassword);

                // Act
                attribute.OnAuthorization(context);

                // Assert
                Assert.Null(context.Result);
            }

            [Fact]
            public void RedirectsToHomepageForAuthenticatedUserWithDiscontinuedPassword()
            {
                var context = BuildAuthorizationContext(BuildClaimsIdentity(AuthenticationTypes.LocalUser, authenticated: true, hasDiscontinuedPasswordClaim: true).Object).Object;
                var attribute = new UiAuthorizeAttribute();

                // Act
                attribute.OnAuthorization(context);

                // Assert
                var redirectResult = context.Result as RedirectToRouteResult;
                Assert.NotNull(redirectResult);
                Assert.True(redirectResult.RouteValues.Contains(new KeyValuePair<string, object>("controller", "Pages")));
                Assert.True(redirectResult.RouteValues.Contains(new KeyValuePair<string, object>("action", "Home")));
            }

            private static Mock<ClaimsIdentity> BuildClaimsIdentity(string authType, bool authenticated, bool hasDiscontinuedPasswordClaim)
            {
                var mockIdentity = new Mock<ClaimsIdentity>();

                mockIdentity.SetupGet(i => i.Claims).Returns(
                    hasDiscontinuedPasswordClaim ? 
                        new[] { new Claim(NuGetClaims.DiscontinuedPassword, NuGetClaims.DiscontinuedPasswordValue) } : 
                        new Claim[0]);
                mockIdentity.SetupGet(i => i.IsAuthenticated).Returns(authenticated);
                mockIdentity.SetupGet(i => i.AuthenticationType).Returns(authType);

                return mockIdentity;
            }

            private static Mock<AuthorizationContext> BuildAuthorizationContext(ClaimsIdentity identity)
            {
                var mockController = new Mock<AppController>();

                var mockHttpContext = new Mock<HttpContextBase>();
                mockHttpContext.SetupGet(c => c.Items).Returns(new Dictionary<object, object> {
                    { "owin.Environment", new Dictionary<string, object>() }
                });

                mockHttpContext.SetupGet(c => c.User.Identity).Returns(identity);
                mockHttpContext.SetupGet(c => c.Response.Cache).Returns(new Mock<HttpCachePolicyBase>().Object);

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