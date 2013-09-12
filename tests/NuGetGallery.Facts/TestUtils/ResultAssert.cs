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
            var redirect = Assert.IsType<RedirectToRouteResult>(result);
            DictionariesMatch(new RouteValueDictionary(expectedRouteData), redirect.RouteValues);
            return redirect;
        }

        public static ViewResult IsView(ActionResult result, string viewName = "", string masterName = "", object model = null, object viewData = null)
        {
            var view = Assert.IsType<ViewResult>(result);

            Assert.Equal(viewName, view.ViewName);
            Assert.Equal(masterName, view.MasterName);
            Assert.Equal(model, view.ViewData.Model);

            if (viewData != null)
            {
                DictionariesMatch(new RouteValueDictionary(viewData), view.ViewData);
            }
            else
            {
                Assert.Equal(0, view.ViewData.Count);
            }
            return view;
        }

        public static HttpStatusCodeResult IsStatusCode(ActionResult result, HttpStatusCode statusCode)
        {
            return IsStatusCode(result, statusCode, statusDescription: null);
        }

        public static HttpStatusCodeResult IsStatusCode(ActionResult result, HttpStatusCode statusCode, string statusDescription)
        {
            var statusResult = Assert.IsType<HttpStatusCodeResult>(result);
            Assert.Equal((int)statusCode, statusResult.StatusCode);
            Assert.Equal(statusDescription, statusResult.StatusDescription);
            return statusResult;
        }

        public static HttpStatusCodeResult IsStatusCodeWithBody(ActionResult result, HttpStatusCode statusCode, string statusDescription)
        {
            return IsStatusCodeWithBody(result, statusCode, statusDescription, body: statusDescription);
        }

        public static HttpStatusCodeWithBodyResult IsStatusCodeWithBody(ActionResult result, HttpStatusCode statusCode, string statusDescription, string body)
        {
            var statusResult = Assert.IsType<HttpStatusCodeWithBodyResult>(result);
            Assert.Equal((int)statusCode, statusResult.StatusCode);
            Assert.Equal(statusDescription, statusResult.StatusDescription);
            Assert.Equal(body, statusResult.Body);
            return statusResult;
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
    }
}
