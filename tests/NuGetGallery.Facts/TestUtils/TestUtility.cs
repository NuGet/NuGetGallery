// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Net.Mail;
using System.Reflection;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using Moq;
using NuGet;

namespace NuGetGallery
{
    public static class TestUtility
    {
        public static readonly string FakeUserName = "theUsername";
        public static readonly string FakeAdminName = "theAdmin";

        public static readonly User FakeUser = new User() { Username = FakeUserName, Key = 42 };
        public static readonly User FakeAdminUser = new User() { Username = FakeAdminName, Roles = new List<Role>() { new Role() { Name = Constants.AdminRoleName } } };

        // We only need this method because testing URL generation is a pain.
        // Alternatively, we could write our own service for generating URLs.
        public static Mock<HttpContextBase> SetupHttpContextMockForUrlGeneration(Mock<HttpContextBase> httpContext, Controller controller)
        {
            httpContext.Setup(c => c.Request.Url).Returns(new Uri("https://example.org/"));
            httpContext.Setup(c => c.Request.ApplicationPath).Returns("/");
            httpContext.Setup(c => c.Response.ApplyAppPathModifier(It.IsAny<string>())).Returns<string>(s => s);
            var requestContext = new RequestContext(httpContext.Object, new RouteData());
            var controllerContext = new ControllerContext(requestContext, controller);
            controller.ControllerContext = controllerContext;
            var routeCollection = new RouteCollection();
            routeCollection.MapRoute("catch-all", "{*catchall}");
            controller.Url = new UrlHelper(requestContext, routeCollection);
            return httpContext;
        }

        public static void SetupUrlHelper(Controller controller, Mock<HttpContextBase> mockHttpContext)
        {
            var routes = new RouteCollection();
            Routes.RegisterRoutes(routes);
            controller.Url = new UrlHelper(new RequestContext(mockHttpContext.Object, new RouteData()), routes);
        }

        public static UrlHelper MockUrlHelper()
        {
            var mockHttpContext = new Mock<HttpContextBase>(MockBehavior.Strict);
            var mockHttpRequest = new Mock<HttpRequestBase>(MockBehavior.Strict);
            var mockHttpResponse = new Mock<HttpResponseBase>(MockBehavior.Strict);
            mockHttpContext.Setup(httpContext => httpContext.Request).Returns(mockHttpRequest.Object);
            mockHttpContext.Setup(httpContext => httpContext.Response).Returns(mockHttpResponse.Object);
            mockHttpRequest.Setup(httpRequest => httpRequest.Url).Returns(new Uri("http://unittest.nuget.org/"));
            mockHttpRequest.Setup(httpRequest => httpRequest.ApplicationPath).Returns("http://unittest.nuget.org/");
            mockHttpRequest.Setup(httpRequest => httpRequest.ServerVariables).Returns(new NameValueCollection());

            string value = null;
            Action<string> saveValue = x =>
            {
                value = x;
            };
            Func<String> restoreValue = () => value;
            mockHttpResponse.Setup(httpResponse => httpResponse.ApplyAppPathModifier(It.IsAny<string>()))
                            .Callback(saveValue).Returns(restoreValue);
            var requestContext = new RequestContext(mockHttpContext.Object, new RouteData());
            var routes = new RouteCollection();
            Routes.RegisterRoutes(routes);
            return new UrlHelper(requestContext, routes);
        }

        public static void SetupUrlHelperForUrlGeneration(Controller controller, Uri address)
        {
            var mockHttpContext = new Mock<HttpContextBase>();
            mockHttpContext.Setup(c => c.Request.Url).Returns(address);
            mockHttpContext.Setup(c => c.Request.ApplicationPath).Returns("/");
            mockHttpContext.Setup(c => c.Response.ApplyAppPathModifier(It.IsAny<string>())).Returns<string>(s => s);

            var requestContext = new RequestContext(mockHttpContext.Object, new RouteData());

            var controllerContext = new ControllerContext(requestContext, controller);
            controller.ControllerContext = controllerContext;

            var routes = new RouteCollection();
            Routes.RegisterRoutes(routes);
            controller.Url = new UrlHelper(requestContext, routes);
        }

        public static T GetAnonymousPropertyValue<T>(Object source, string propertyName)
        {
            var property = source.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.DeclaredOnly | BindingFlags.Public);
            if (property == null)
            {
                return default(T);
            }
            return (T)property.GetValue(source, null);
        }

        public static Stream CreateTestStream(string content)
        {
            return new MemoryStream(Encoding.UTF8.GetBytes(content));
        }
    }
}