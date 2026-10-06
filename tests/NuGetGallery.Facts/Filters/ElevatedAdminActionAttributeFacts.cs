// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Web;
using System.Web.Mvc;
using Moq;
using NuGetGallery.Areas.Admin.Controllers;
using NuGetGallery.Configuration;
using Xunit;
using AuthorizationContext = System.Web.Mvc.AuthorizationContext;

namespace NuGetGallery.Filters
{
    public class ElevatedAdminActionAttributeFacts
    {
        private const string AdminsRole = CoreConstants.AdminRoleName;
        private const string ElevatedAdminRole = CoreConstants.ElevatedAdminRoleName;

        public class TheOnAuthorizationMethod
        {
            [Fact]
            public void Returns403WhenAdminPanelDisabled()
            {
                // Arrange:
                SetupConfigService(adminPanelEnabled: false);
                var context = BuildAuthorizationContext(new[] { AdminsRole, ElevatedAdminRole }).Object;
                var attribute = new ElevatedAdminActionAttribute();

                // Act
                attribute.OnAuthorization(context);

                // Assert
                var result = context.Result as HttpStatusCodeResult;
                Assert.NotNull(result);
                Assert.Equal(403, result.StatusCode);
            }

            [Theory]
            [InlineData(new string[0], false)]                                 // no roles -> denied
            [InlineData(new[] { AdminsRole }, false)]                          // Admins only -> denied (missing ElevatedAdmin)
            [InlineData(new[] { ElevatedAdminRole }, true)]                    // ElevatedAdmin present -> filter allows
            [InlineData(new[] { AdminsRole, ElevatedAdminRole }, true)]        // both -> filter allows
            public void EnforcesElevatedAdminRole(string[] roles, bool expectAllowed)
            {
                // Arrange
                SetupConfigService(adminPanelEnabled: true);
                var context = BuildAuthorizationContext(roles).Object;
                var attribute = new ElevatedAdminActionAttribute();

                // Act
                attribute.OnAuthorization(context);

                // Assert
                if (expectAllowed)
                {
                    Assert.Null(context.Result);
                }
                else
                {
                    Assert.IsType<HttpUnauthorizedResult>(context.Result);
                }
            }
        }

        public class TheStackedDeleteRouteAuthorization
        {
            // Mirrors how the admin delete routes stack [AdminAction] (requires Admins)
            // and [ElevatedAdminAction] (requires ElevatedAdmin): both must pass.
            [Theory]
            [InlineData(new string[0], false)]                                 // neither -> denied
            [InlineData(new[] { AdminsRole }, false)]                          // Admins only -> denied
            [InlineData(new[] { ElevatedAdminRole }, false)]                   // ElevatedAdmin only -> denied
            [InlineData(new[] { AdminsRole, ElevatedAdminRole }, true)]        // both -> allowed
            public void RequiresBothAdminsAndElevatedAdmin(string[] roles, bool expectAllowed)
            {
                // Arrange
                SetupConfigService(adminPanelEnabled: true);
                var context = BuildAuthorizationContext(roles).Object;

                // Act: run the stacked filters in order, short-circuiting like MVC does.
                RunFilters(context, new AdminActionAttribute(), new ElevatedAdminActionAttribute());

                // Assert
                if (expectAllowed)
                {
                    Assert.Null(context.Result);
                }
                else
                {
                    Assert.IsType<HttpUnauthorizedResult>(context.Result);
                }
            }

            private static void RunFilters(AuthorizationContext context, params IAuthorizationFilter[] filters)
            {
                foreach (var filter in filters)
                {
                    if (context.Result != null)
                    {
                        break;
                    }

                    filter.OnAuthorization(context);
                }
            }
        }

        public class TheDeleteRoutesRequireBothRoles
        {
            [Fact]
            public void PackagesControllerDeleteRequiresAdminsAndElevatedAdmin()
            {
                var method = typeof(PackagesController).GetMethod(
                    nameof(PackagesController.Delete),
                    new[] { typeof(DeletePackagesRequest) });

                Assert.NotNull(method);
                Assert.True(HasAttribute<AdminActionAttribute>(method),
                    "Delete must keep [AdminAction] so the Admins role remains required.");
                Assert.True(HasAttribute<ElevatedAdminActionAttribute>(method),
                    "Delete must keep [ElevatedAdminAction] so the ElevatedAdmin role remains required.");
            }

            [Theory]
            [InlineData(nameof(DeleteController.Index))]
            [InlineData(nameof(DeleteController.Search))]
            public void DeleteControllerActionsRequireAdminsAndElevatedAdmin(string actionName)
            {
                var method = typeof(DeleteController).GetMethods().Single(m => m.Name == actionName);

                // ElevatedAdmin comes from the method-level filter.
                Assert.True(HasAttribute<ElevatedAdminActionAttribute>(method),
                    $"{actionName} must keep [ElevatedAdminAction].");

                // Admins comes from the class-level [UIAuthorize(Roles="Admins")] on AdminControllerBase.
                var classAuthorize = typeof(DeleteController)
                    .GetCustomAttributes(typeof(UIAuthorizeAttribute), inherit: true)
                    .Cast<UIAuthorizeAttribute>();
                Assert.Contains(classAuthorize, a => a.Roles != null && a.Roles.Contains(CoreConstants.AdminRoleName));
            }

            private static bool HasAttribute<T>(MethodInfo method) where T : Attribute
            {
                return method.GetCustomAttributes(typeof(T), inherit: true).Any();
            }
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

            // AdminHelper caches the admin-panel flag in a static Lazy<bool> that is
            // evaluated once per process. Reset it so each test observes the value
            // configured above regardless of test execution order.
            var lazyField = typeof(AdminHelper).GetField(
                "AdminPanelEnabled",
                BindingFlags.NonPublic | BindingFlags.Static);
            lazyField.SetValue(null, new Lazy<bool>(() => adminPanelEnabled));
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
