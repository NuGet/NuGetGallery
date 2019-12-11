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
using AuthorizationContext = System.Web.Mvc.AuthorizationContext;
using AuthenticationTypes = NuGetGallery.Authentication.AuthenticationTypes;

namespace NuGetGallery.Filters
{
    public class UIAuthorizeAttributeFacts
    {
        public class TheOnAuthorizationMethod
        {
            private static IEnumerable<string> AuthTypes = new[]
            {
                AuthenticationTypes.LocalUser,
                AuthenticationTypes.ApiKey,
                AuthenticationTypes.External
            };

            public static IEnumerable<object[]> AllowsDiscontinuedLogins_Data
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
                    foreach (var allowsDiscontinuedLogin in new[] { false, true })
                    {
                        foreach (var authType in AuthTypes)
                        {
                            yield return MemberDataHelper.AsData(
                                allowsDiscontinuedLogin, 
                                BuildClaimsIdentity(
                                    authType, 
                                    authenticated: false, 
                                    hasDiscontinuedLoginClaim: false).Object);

                            yield return MemberDataHelper.AsData(
                                allowsDiscontinuedLogin, 
                                BuildClaimsIdentity(
                                    authType, 
                                    authenticated: false, 
                                    hasDiscontinuedLoginClaim: true).Object);
                        }
                    }
                }
            }

            [Theory]
            [MemberData(nameof(FailsForUnauthenticatedUser_Data))]
            public void FailsForUnauthenticatedUser(bool allowsDiscontinuedLogin, ClaimsIdentity identity)
            {
                var context = BuildAuthorizationContext(identity).Object;
                var attribute = new UIAuthorizeAttribute(allowsDiscontinuedLogin);

                // Act
                attribute.OnAuthorization(context);

                // Assert
                Assert.IsType<HttpUnauthorizedResult>(context.Result);
            }

            public static IEnumerable<object[]> SucceedsForAuthenticatedUserWithoutDiscontinuedLogin_Data =>
                MemberDataHelper
                    .Combine(
                        AllowsDiscontinuedLogins_Data,
                        AuthTypes.Select(t => MemberDataHelper.AsData(t, false)))
                    .Concat(
                        AuthTypes.Select(t => MemberDataHelper.AsData(true, t, true)));

            [Theory]
            [MemberData(nameof(SucceedsForAuthenticatedUserWithoutDiscontinuedLogin_Data))]
            public void SucceedsForAuthenticatedUserWithoutDiscontinuedLogin(
                bool allowsDiscontinuedLogin, string authType, bool hasDiscontinuedLoginClaim)
            {
                var context = BuildAuthorizationContext(
                    BuildClaimsIdentity(
                        authType, 
                        authenticated: true, 
                        hasDiscontinuedLoginClaim: hasDiscontinuedLoginClaim).Object).Object;
                var attribute = new UIAuthorizeAttribute(allowsDiscontinuedLogin);

                // Act
                attribute.OnAuthorization(context);

                // Assert
                Assert.Null(context.Result);
            }

            public static IEnumerable<object[]> RedirectsToHomepageForAuthenticatedUserWithDiscontinuedLogin_Data => 
                AuthTypes.Select(t => MemberDataHelper.AsData(t));

            [Theory]
            [MemberData(nameof(RedirectsToHomepageForAuthenticatedUserWithDiscontinuedLogin_Data))]
            public void RedirectsToHomepageForAuthenticatedUserWithDiscontinuedLogin(string authType)
            {
                var context = BuildAuthorizationContext(
                    BuildClaimsIdentity(
                        authType, 
                        authenticated: true, 
                        hasDiscontinuedLoginClaim: true).Object).Object;
                var attribute = new UIAuthorizeAttribute();

                // Act
                attribute.OnAuthorization(context);

                // Assert
                var redirectResult = context.Result as RedirectToRouteResult;
                Assert.NotNull(redirectResult);
                Assert.Contains(new KeyValuePair<string, object>("controller", "Pages"), redirectResult.RouteValues);
                Assert.Contains(new KeyValuePair<string, object>("action", "Home"), redirectResult.RouteValues);
            }

            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public void RedirectsToHomepageOnlyWith2FAMarker(bool shouldEnable2FA)
            {
                var tempData = new TempDataDictionary();
                if (shouldEnable2FA)
                {
                    tempData.Add(GalleryConstants.AskUserToEnable2FA, true);
                }

                var context = BuildAuthorizationContext(
                    BuildClaimsIdentity(
                        AuthenticationTypes.External,
                        authenticated: true,
                        hasDiscontinuedLoginClaim: false).Object).Object;
                context.Controller.TempData = tempData;
                var attribute = new UIAuthorizeAttribute();

                // Act
                attribute.OnAuthorization(context);

                // Assert
                var redirectResult = context.Result as RedirectToRouteResult;
                if (shouldEnable2FA)
                {
                    Assert.NotNull(redirectResult);
                    Assert.Contains(new KeyValuePair<string, object>("controller", "Pages"), redirectResult.RouteValues);
                    Assert.Contains(new KeyValuePair<string, object>("action", "Home"), redirectResult.RouteValues);
                }
                else
                {
                    Assert.Null(redirectResult);
                }
            }

            private static Mock<ClaimsIdentity> BuildClaimsIdentity(string authType, bool authenticated, bool hasDiscontinuedLoginClaim)
            {
                var mockIdentity = new Mock<ClaimsIdentity>();
                var claims = new List<Claim>();
                if (hasDiscontinuedLoginClaim)
                {
                    ClaimsExtensions.AddBooleanClaim(claims, NuGetClaims.DiscontinuedLogin);
                }

                mockIdentity.SetupGet(i => i.Claims).Returns(claims.ToArray());
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