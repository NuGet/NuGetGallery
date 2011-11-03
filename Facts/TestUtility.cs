using System;
using System.Reflection;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using Moq;

namespace NuGetGallery
{
    public static class TestUtility
    {
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

        public static T GetAnonymousPropertyValue<T>(Object source, string propertyName)
        {
            var property = source.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.DeclaredOnly | BindingFlags.Public);
            if (property == null)
            {
                return default(T);
            }
            return (T)property.GetValue(source, null);
        }
    }
}
