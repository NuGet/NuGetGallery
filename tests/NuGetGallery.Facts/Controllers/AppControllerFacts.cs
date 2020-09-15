// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net;
using System.Web;
using System.Web.Mvc;
using System.Reflection;
using System.Collections.Generic;
using Moq;
using Xunit;
using NuGetGallery.Cookies;
using NuGetGallery.Framework;
using NuGet.Services.Entities;

namespace NuGetGallery.Controllers
{
    public class AppControllerFacts
    {
        public class TheGetCurrentUserMethod
        {
            [Fact]
            public void GivenNoActiveUserPrincipal_ItReturnsNull()
            {
                // Arrange
                var ctrl = new TestableAppController();
                ctrl.SetOwinContextOverride(Fakes.CreateOwinContext());

                // Act
                var user = ctrl.InvokeGetCurrentUser();

                // Assert
                Assert.Null(user);
            }
        }

        public class TheJsonMethod : TestContainer
        {
            [Fact]
            public void AllowsJsonRequestBehaviorToBeSpecified()
            {
                // Arrange
                var controller = GetController<TestableAppController>();

                // Act
                var output = controller.Json(HttpStatusCode.BadRequest, null, JsonRequestBehavior.AllowGet);

                // Assert
                Assert.Equal(JsonRequestBehavior.AllowGet, output.JsonRequestBehavior);
            }

            [Fact]
            public void DefaultsToDenyGet()
            {
                // Arrange
                var controller = GetController<TestableAppController>();

                // Act
                var output = controller.Json(HttpStatusCode.BadRequest, null);

                // Assert
                Assert.Equal(JsonRequestBehavior.DenyGet, output.JsonRequestBehavior);
            }
        }

        public class TheOnActionExecutedMethod : TestContainer
        {
            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public void SetViewBagGivenHttpContextItemsWithCanWriteAnalyticsCookies(bool canWriteAnalyticsCookies)
            {
                // Arrange
                var controller = GetController<TestableAppController>();
                var cookieComplianceService = new Mock<ICookieComplianceService>();
                cookieComplianceService.Setup(c => c.ExpireAnalyticsCookies(It.IsAny<HttpContextBase>()));
                controller.SetCookieComplianceService(cookieComplianceService.Object);

                var httpContext = new Mock<HttpContextBase>();
                var items = new Dictionary<string, bool>
                {
                    ["CanWriteAnalyticsCookies"] = canWriteAnalyticsCookies
                };
                httpContext.Setup(c => c.Items).Returns(items);

                // Act
                InvokeOnActionExecutedMethod(controller.ControllerContext, httpContext.Object, controller);

                // Assert
                Assert.Equal(canWriteAnalyticsCookies, controller.ViewBag.CanWriteAnalyticsCookies);
                if (canWriteAnalyticsCookies)
                {
                    cookieComplianceService.Verify(c => c.ExpireAnalyticsCookies(It.IsAny<HttpContextBase>()), Times.Never);
                } else
                {
                    cookieComplianceService.Verify(c => c.ExpireAnalyticsCookies(It.IsAny<HttpContextBase>()), Times.Once);
                }
            }

            [Fact]
            public void SetViewBagGivenHttpContextItemsWithNoCanWriteAnalyticsCookies()
            {
                // Arrange
                var controller = GetController<TestableAppController>();
                var cookieComplianceService = new Mock<ICookieComplianceService>();
                cookieComplianceService.Setup(c => c.ExpireAnalyticsCookies(It.IsAny<HttpContextBase>()));
                controller.SetCookieComplianceService(cookieComplianceService.Object);

                var httpContext = new Mock<HttpContextBase>();
                var items = new Dictionary<string, bool>();
                httpContext.Setup(c => c.Items).Returns(items);

                // Act
                InvokeOnActionExecutedMethod(controller.ControllerContext, httpContext.Object, controller);

                // Assert
                Assert.False(controller.ViewBag.CanWriteAnalyticsCookies);
                cookieComplianceService.Verify(c => c.ExpireAnalyticsCookies(It.IsAny<HttpContextBase>()), Times.Once);
            }

            private void InvokeOnActionExecutedMethod(ControllerContext controllerContext, HttpContextBase httpContext, AppController controller)
            {
                var actionExecutedContext = new ActionExecutedContext(controllerContext,
                  Mock.Of<ActionDescriptor>(), false, null)
                {
                    HttpContext = httpContext
                };

                MethodInfo onActionExecuted = controller.GetType().GetMethod(
                  "OnActionExecuted", BindingFlags.Instance | BindingFlags.NonPublic);

                onActionExecuted.Invoke(controller, new object[] { actionExecutedContext });
            }
        }

        public class TestableAppController : AppController
        {
            public User InvokeGetCurrentUser()
            {
                return GetCurrentUser();
            }
        }
    }
}
