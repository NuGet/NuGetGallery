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
            return IsStatusCode(result, (int)statusCode);
        }

        public static HttpStatusCodeResult IsStatusCode(ActionResult result, int statusCode)
        {
            if (result is EmptyResult)
            {
                Assert.Equal(statusCode, 200);
                return null;
            }

            return IsStatusCode(result, statusCode, statusDescription: null);
        }

        public static HttpStatusCodeResult IsStatusCode(ActionResult result, HttpStatusCode statusCode, string statusDescription)
        {
            return IsStatusCode(result, (int)statusCode, statusDescription);
        }

        public static HttpStatusCodeResult IsStatusCode(ActionResult result, int statusCode, string statusDescription)
        {
            var statusResult = Assert.IsAssignableFrom<HttpStatusCodeResult>(result);
            Assert.Equal(statusCode, statusResult.StatusCode);
            Assert.Equal(statusDescription, statusResult.StatusDescription);
            return statusResult;
        }

        public static HttpStatusCodeResult IsStatusCodeWithBody(ActionResult result, HttpStatusCode statusCode, string statusDescription)
        {
            return IsStatusCodeWithBody(result, (int)statusCode, statusDescription, body: statusDescription);
        }

        public static HttpStatusCodeResult IsStatusCodeWithBody(ActionResult result, int statusCode, string statusDescription)
        {
            return IsStatusCodeWithBody(result, statusCode, statusDescription, body: statusDescription);
        }

        public static HttpStatusCodeWithBodyResult IsStatusCodeWithBody(ActionResult result, HttpStatusCode statusCode, string statusDescription, string body)
        {
            return IsStatusCodeWithBody(result, (int)statusCode, statusDescription, body);
        }

        public static HttpStatusCodeWithBodyResult IsStatusCodeWithBody(ActionResult result, int statusCode, string statusDescription, string body)
        {
            var statusResult = Assert.IsAssignableFrom<HttpStatusCodeWithBodyResult>(result);
            Assert.Equal(statusCode, statusResult.StatusCode);
            Assert.Equal(statusDescription, statusResult.StatusDescription);
            Assert.Equal(body, statusResult.Body);
            return statusResult;
        }
    }
}

