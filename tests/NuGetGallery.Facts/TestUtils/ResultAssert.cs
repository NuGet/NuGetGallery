using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web.Mvc;
using System.Web.Routing;
using Xunit;

namespace NuGetGallery
{
    public static class ResultAssert
    {
        public static RedirectResult IsRedirectTo(ActionResult result, string expectedUrl)
        {
            var redirect = Assert.IsType<RedirectResult>(result);
            Assert.Equal(expectedUrl, redirect.Url);
            return redirect;
        }

        public static RedirectToRouteResult IsRedirectToRoute(ActionResult result, object expectedRouteData)
        {
            return IsRedirectToRoute(result, expectedRouteData, permanent: false);
        }

        public static RedirectToRouteResult IsRedirectToRoute(ActionResult result, object expectedRouteData, bool permanent)
        {
            return IsRedirectToRoute(result, expectedRouteData, permanent, routeName: String.Empty);
        }

        public static RedirectToRouteResult IsRedirectToRoute(ActionResult result, object expectedRouteData, string routeName)
        {
            return IsRedirectToRoute(result, expectedRouteData, permanent: false, routeName: routeName);
        }

        public static RedirectToRouteResult IsRedirectToRoute(ActionResult result, object expectedRouteData, bool permanent, string routeName)
        {
            var redirect = Assert.IsType<RedirectToRouteResult>(result);
            DictionariesMatch(new RouteValueDictionary(expectedRouteData), redirect.RouteValues);
            Assert.Equal(permanent, redirect.Permanent);
            Assert.Equal(routeName, redirect.RouteName);
            return redirect;
        }

        public static ViewResult IsView(ActionResult result, string viewName = "", string masterName = "", object viewData = null)
        {
            var view = Assert.IsType<ViewResult>(result);

            Assert.Equal(viewName, view.ViewName);
            Assert.Equal(masterName, view.MasterName);
            
            if (viewData != null)
            {
                DictionariesMatch(new RouteValueDictionary(viewData), view.ViewData);
            }
            return view;
        }

        public static T IsView<T>(ActionResult result, string viewName = "", string masterName = "", object viewData = null)
        {
            var view = IsView(result, viewName, masterName, viewData);
            return Assert.IsType<T>(view.Model);
        }

        public static HttpNotFoundResult IsNotFound(ActionResult result)
        {
            return Assert.IsType<HttpNotFoundResult>(result);
        }

        private static void DictionariesMatch<K, V>(IDictionary<K, V> expected, IDictionary<K, V> actual)
        {
            var expectedKeys = expected.Keys.Cast<object>().ToList();

            foreach (var key in actual.Keys)
            {
                Assert.True(expected.ContainsKey(key), "Unexpected key found: " + key);
                Assert.Equal(expected[key], actual[key]);
                expectedKeys.Remove(key);
            }

            // Make sure we used all the expected keys (Assert.True lets us provide a message)
            Assert.True(expectedKeys.Count == 0, "Missing keys: " + String.Join(",", expectedKeys));
        }

        public static HttpStatusCodeResult IsStatusCode(ActionResult result, HttpStatusCode statusCode)
        {
            IsStatusCode(result, (int)code, description: null);
        }

        public static HttpStatusCodeWithBodyResult IsStatusCodeWithBody(ActionResult result, int statusCode, string statusDescription, string body)
        {
            IsStatusCode(result, code, description: null);
        }

        public static void IsStatusCode(ActionResult result, HttpStatusCode code, string description)
        {
            IsStatusCode(result, (int)code, description);
        }

        public static void IsStatusCode(ActionResult result, int code, string description)
        {
            var statusCodeResult = Assert.IsAssignableFrom<HttpStatusCodeResult>(result);
            Assert.Equal(code, statusCodeResult.StatusCode);
            Assert.Equal(description, statusCodeResult.StatusDescription);
        }

        public static EmptyResult IsEmpty(ActionResult result)
        {
            return Assert.IsType<EmptyResult>(result);
        }
    }
}
