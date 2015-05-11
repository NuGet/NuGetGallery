// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
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

        public static SafeRedirectResult IsSafeRedirectTo(ActionResult result, string expectedUrl)
        {
            var redirect = Assert.IsType<SafeRedirectResult>(result);
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

        public static TModel IsView<TModel>(ActionResult result, string viewName = "", string masterName = "", object viewData = null)
        {
            var model = Assert.IsType<TModel>(IsView(result, viewName, masterName, viewData).Model);
            return model;
        }

        public static HttpNotFoundResult IsNotFound(ActionResult result)
        {
            return Assert.IsType<HttpNotFoundResult>(result);
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

        public static HttpStatusCodeResult IsStatusCode(ActionResult result, HttpStatusCode statusCode)
        {
            return IsStatusCode(result, (int)statusCode, description: null);
        }

        public static HttpStatusCodeResult IsStatusCode(ActionResult result, int statusCode)
        {
            return IsStatusCode(result, statusCode, description: null);
        }

        public static HttpStatusCodeResult IsStatusCode(ActionResult result, HttpStatusCode statusCode, string description)
        {
            return IsStatusCode(result, (int)statusCode, description);
        }

        public static HttpStatusCodeResult IsStatusCode(ActionResult result, int statusCode, string description)
        {
            var statusCodeResult = Assert.IsAssignableFrom<HttpStatusCodeResult>(result);
            Assert.Equal(statusCode, statusCodeResult.StatusCode);
            Assert.Equal(description, statusCodeResult.StatusDescription);
            return statusCodeResult;
        }

        public static EmptyResult IsEmpty(ActionResult result)
        {
            return Assert.IsType<EmptyResult>(result);
        }

        public static ChallengeResult IsChallengeResult(ActionResult result, string provider, string redirectUrl)
        {
            var challenge = Assert.IsType<ChallengeResult>(result);
            Assert.Equal(redirectUrl, challenge.RedirectUri);
            Assert.Equal(provider, challenge.LoginProvider);
            return challenge;
        }
    }
}

