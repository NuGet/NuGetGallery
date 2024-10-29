// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using Moq;
using NuGet.Services.Entities;
using NuGetGallery.Framework;
using Xunit;

namespace NuGetGallery.Filters
{

    public class RequiresAccountConfirmationAttributeFacts
        : TestContainer
    {
        [Fact]
        public void RequiresAccountConfirmationAttributeThrowsExceptionIfContextNull()
        {
            var attribute = new RequiresAccountConfirmationAttribute("some string");

            // Act/Assert
            Assert.Throws<ArgumentNullException>(() => attribute.OnActionExecuting(null));
        }

        [Fact]
        public void RequiresAccountConfirmationAttributeThrowsExceptionIfNotAuthenticated()
        {
            var mockActionContext = new Mock<ActionExecutingContext>(MockBehavior.Strict);
            mockActionContext.SetupGet(x => x.HttpContext.Request.IsAuthenticated).Returns(false);

            // Act
            var attribute = new RequiresAccountConfirmationAttribute("some string");

            // Act/Assert
            var exception = Assert.Throws<InvalidOperationException>(() => attribute.OnActionExecuting(mockActionContext.Object));
            Assert.Equal("Requires account confirmation attribute is only valid on authenticated actions.", exception.Message);
        }

        [Fact]
        public void RequiresAccountConfirmationAttributePassedWhenUserNotConfirmed()
        {
            var controller = new Mock<AppController>();
            controller.Setup(x => x.GetCurrentUser()).Returns(new User { EmailAddress = "test@mail.com" });
            var mockActionContext = new Mock<ActionExecutingContext>(MockBehavior.Strict);
            mockActionContext.SetupGet(x => x.HttpContext.Request.IsAuthenticated).Returns(true);
            mockActionContext.SetupGet(x => x.Controller).Returns(controller.Object);

            var attribute = new RequiresAccountConfirmationAttribute("some string");

            // Act
            attribute.OnActionExecuting(mockActionContext.Object);

            var result = controller.Object.TempData;

            // Assert
            Assert.Null(result["ConfirmationRequiredMessage"]);

            controller.Verify(x => x.GetCurrentUser());
            mockActionContext.Verify(x => x.HttpContext.Request.IsAuthenticated);
            mockActionContext.Verify(x => x.Controller);
        }

        [Theory]
        [InlineData("undo pending edits")]
        [InlineData("contact support about your package")]
        [InlineData("accept ownership of a package")]
        [InlineData("edit a package")]
        [InlineData("contact package owners")]
        [InlineData("upload a package")]
        [InlineData("unlist a package")]
        public void RequiresAccountConfirmationAttributePassedWithConfirmationRequiredMessage(string inOrderTo)
        {
            var cookieCollection = new HttpCookieCollection();
            var response = new Mock<HttpResponseBase>(MockBehavior.Strict);
            response.SetupGet(x => x.Cookies).Returns(cookieCollection);
            response.Setup(x => x.ApplyAppPathModifier("/account/ConfirmationRequired")).Returns<string>(x => x);

            var request = new Mock<HttpRequestBase>();
            request.Setup(m => m.Url).Returns(new Uri(TestUtility.GallerySiteRootHttps));
            request.Setup(m => m.RawUrl).Returns(TestUtility.GallerySiteRootHttps);
            request.Setup(m => m.IsSecureConnection).Returns(true);

            var httpContext = new Mock<HttpContextBase>();
            httpContext.SetupGet(h => h.Request).Returns(request.Object);
            httpContext.SetupGet(x => x.Response).Returns(response.Object);

            var routeCollection = new RouteCollection();
            Routes.RegisterRoutes(routeCollection);

            var requestContext = new RequestContext(httpContext.Object, new RouteData());
            var urlHelper = new UrlHelper(requestContext, routeCollection);

            var controller = new TestableAppController { Url = urlHelper };
            controller.ControllerContext = new ControllerContext(requestContext, controller);
            var mockActionContext = new Mock<ActionExecutingContext>(MockBehavior.Strict);
            mockActionContext.SetupGet(x => x.HttpContext.Request.IsAuthenticated).Returns(true);
            mockActionContext.SetupGet(x => x.Controller).Returns(controller);

            var attribute = new RequiresAccountConfirmationAttribute(inOrderTo);

            // Act
            attribute.OnActionExecuting(mockActionContext.Object);

            var result = mockActionContext.Object.Result;

            // Assert
            Assert.IsType<RedirectResult>(result);
            Assert.Equal(TestUtility.GallerySiteRootHttps + "account/ConfirmationRequired", ((RedirectResult)result).Url);
            Assert.Equal(string.Format(CultureInfo.CurrentCulture, "Before you can {0} you must first confirm your email address.", inOrderTo), controller.TempData["ConfirmationRequiredMessage"]);
            Assert.Equal("ConfirmationContext", cookieCollection[0].Name);
            Assert.NotEmpty(cookieCollection[0].Value);

            mockActionContext.Verify(x => x.HttpContext.Request.IsAuthenticated);
            mockActionContext.Verify(x => x.Controller);
        }

        public class TestableAppController : AppController
        {
            protected internal override User GetCurrentUser()
            {
                return new User();
            }
        }
    }
}