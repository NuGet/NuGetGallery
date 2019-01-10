// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Specialized;
using System.IO;
using System.Reflection;
using System.Text;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using Moq;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public static class TestUtility
    {
        private static int _key = 42;
        private static readonly string galleryHostName = "localhost";

        public static readonly string GallerySiteRootHttp = $"http://{galleryHostName}/";
        public static readonly string GallerySiteRootHttps = $"https://{galleryHostName}/";

        public static readonly string FakeUserName = "theUsername";
        public static readonly int FakeUserKey = _key++;
        public static readonly User FakeUser = new User() { Username = FakeUserName, Key = FakeUserKey, EmailAddress = "theUsername@nuget.org" };

        public static readonly string FakeAdminName = "theAdmin";
        public static readonly int FakeAdminKey = _key++;
        public static readonly User FakeAdminUser = new User()
        {
            Username = FakeAdminName,
            Key = FakeAdminKey,
            EmailAddress = "theAdmin@nuget.org",
            Roles = new[]
            {
                new Role { Name = CoreConstants.AdminRoleName }
            }
        };

        public static readonly string FakeOrganizationName = "theOrganization";
        public static readonly int FakeOrganizationKey = _key++;
        public static readonly Organization FakeOrganization;

        public static readonly string FakeOrganizationAdminName = "theOrganizationAdmin";
        public static readonly int FakeOrganizationAdminKey = _key++;
        public static readonly User FakeOrganizationAdmin;

        public static readonly string FakeOrganizationCollaboratorName = "theOrganizationCollaborator";
        public static readonly int FakeOrganizationCollaboratorKey = _key++;
        public static readonly User FakeOrganizationCollaborator;

        static TestUtility()
        {
            // Set up fake Organization users
            FakeOrganization = new Organization { Username = FakeOrganizationName, Key = FakeOrganizationKey, EmailAddress = "organization@nuget.org" };
            FakeOrganizationAdmin = new User { Username = FakeOrganizationAdminName, Key = FakeOrganizationAdminKey, EmailAddress = "organizationAdmin@nuget.org" };
            FakeOrganizationCollaborator = new User { Username = FakeOrganizationCollaboratorName, Key = FakeOrganizationCollaboratorKey, EmailAddress = "organizationCollaborator@nuget.org" };

            var organizationAdminMembership = new Membership { IsAdmin = true, Member = FakeOrganizationAdmin, MemberKey = FakeOrganizationAdmin.Key, Organization = FakeOrganization, OrganizationKey = FakeOrganization.Key };
            FakeOrganizationAdmin.Organizations = new[] { organizationAdminMembership };

            var organizationCollaboratorMembership = new Membership { IsAdmin = false, Member = FakeOrganizationCollaborator, MemberKey = FakeOrganizationCollaborator.Key, Organization = FakeOrganization, OrganizationKey = FakeOrganization.Key };
            FakeOrganizationCollaborator.Organizations = new[] { organizationCollaboratorMembership };

            FakeOrganization.Members = new[] { organizationAdminMembership, organizationCollaboratorMembership };
        }

        // We only need this method because testing URL generation is a pain.
        // Alternatively, we could write our own service for generating URLs.
        public static Mock<HttpContextBase> SetupHttpContextMockForUrlGeneration(Mock<HttpContextBase> httpContext, Controller controller)
        {
            // We default all requests to HTTPS in our tests.
            httpContext.Setup(c => c.Request.Url).Returns(new Uri(GallerySiteRootHttps));
            httpContext.Setup(c => c.Request.ApplicationPath).Returns("/");
            httpContext.Setup(c => c.Response.ApplyAppPathModifier(It.IsAny<string>())).Returns<string>(s => s);
            var requestContext = new RequestContext(httpContext.Object, new RouteData());
            var controllerContext = new ControllerContext(requestContext, controller);
            controller.ControllerContext = controllerContext;
            var routeCollection = new RouteCollection();
            Routes.RegisterRoutes(routeCollection);
            controller.Url = new UrlHelper(requestContext, routeCollection);
            return httpContext;
        }

        public static void SetupUrlHelper(Controller controller, Mock<HttpContextBase> mockHttpContext)
        {
            var routes = new RouteCollection();
            Routes.RegisterRoutes(routes);
            controller.Url = new UrlHelper(new RequestContext(mockHttpContext.Object, new RouteData()), routes);
        }

        public static UrlHelper MockUrlHelper(string siteRoot = null)
        {
            if (string.IsNullOrEmpty(siteRoot))
            {
                siteRoot = GallerySiteRootHttps;
            }

            // We default all requests to HTTPS in our tests.
            var mockHttpContext = new Mock<HttpContextBase>(MockBehavior.Loose);
            var mockHttpRequest = new Mock<HttpRequestBase>(MockBehavior.Strict);
            var mockHttpResponse = new Mock<HttpResponseBase>(MockBehavior.Strict);
            mockHttpContext.Setup(httpContext => httpContext.Request).Returns(mockHttpRequest.Object);
            mockHttpContext.Setup(httpContext => httpContext.Response).Returns(mockHttpResponse.Object);
            mockHttpRequest.Setup(httpRequest => httpRequest.Url).Returns(new Uri(siteRoot));
            mockHttpRequest.Setup(httpRequest => httpRequest.ApplicationPath).Returns("/");
            mockHttpRequest.Setup(httpRequest => httpRequest.ServerVariables).Returns(new NameValueCollection());
            mockHttpRequest.Setup(httpRequest => httpRequest.IsSecureConnection).Returns(false);

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

        public static void SetupUrlHelperForUrlGeneration(Controller controller)
        {
            // We default all requests to HTTPS in our tests.
            var mockHttpContext = new Mock<HttpContextBase>();
            mockHttpContext.Setup(c => c.Request.Url).Returns(new Uri(GallerySiteRootHttps));
            mockHttpContext.Setup(c => c.Request.ApplicationPath).Returns("/");
            mockHttpContext.Setup(c => c.Request.IsSecureConnection).Returns(true);
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