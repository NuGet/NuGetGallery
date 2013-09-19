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

        public static ViewResult IsView(ActionResult result, string viewName = "", string masterName = "", object viewData = null)
        {
            var view = Assert.IsType<ViewResult>(result);

            Assert.Equal(viewName, view.ViewName);
            Assert.Equal(masterName, view.MasterName);

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

        public static TModel IsView<TModel>(ActionResult result, string viewName = "", string masterName = "", object viewData = null)
        {
            var model = Assert.IsType<TModel>(IsView(result, viewName, masterName, viewData).Model);
            return model;
        }

        private static void DictionariesMatch<V>(IDictionary<string, V> expected, IDictionary<string, V> actual)
        {
            var expectedKeys = new HashSet<string>(
                expected.Keys,
                StringComparer.OrdinalIgnoreCase);

            foreach (var key in actual.Keys)
            {
                Assert.True(expected.ContainsKey(key), "Unexpected key found: " + key);
                Assert.Equal(expected[key], actual[key]);
                expectedKeys.Remove(key);
            }

            // Make sure we used all the expected keys (Assert.True lets us provide a message)
            Assert.True(expectedKeys.Count == 0, "Missing keys: " + String.Join(",", expectedKeys));
        }

        public static void IsStatusCode(ActionResult result, HttpStatusCode code)
        {
            IsStatusCode(result, (int)code, description: null);
        }

        public static void IsStatusCode(ActionResult result, int code)
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
