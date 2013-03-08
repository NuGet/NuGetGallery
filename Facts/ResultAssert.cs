using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Mvc;
using System.Web.Routing;
using Xunit;

namespace NuGetGallery
{
    public static class ResultAssert
    {
        public static void IsRedirectTo(string expectedUrl, ActionResult result) {
            var redirect = Assert.IsType<RedirectResult>(result);
            Assert.Equal(expectedUrl, redirect.Url);
        }

        public static void IsRedirectToRoute(object expectedRouteData, ActionResult result)
        {
            var redirect = Assert.IsType<RedirectToRouteResult>(result);

            var rvd = new RouteValueDictionary(expectedRouteData);
            foreach (var key in redirect.RouteValues.Keys)
            {
                Assert.True(rvd.ContainsKey(key), "Unexpected key found: " + key);
                Assert.Equal(rvd[key], redirect.RouteValues[key]);
                rvd.Remove(key);
            }
            
            // Make sure we used all the expected keys (Assert.True lets us provide a message)
            Assert.True(rvd.Count == 0, "Missing keys: " + String.Join(",", rvd.Keys));
        }
    }
}
