// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Web;
using System.Web.Routing;

namespace NuGetGallery.Helpers
{
    public class ObfuscationHelper
    {
        public static string ObfuscateRequestUrl(HttpContextBase httpContext, RouteCollection routes)
        {
            if (httpContext?.Request?.Url == null || routes == null)
            {
                return string.Empty;
            }

            var route = routes.GetRouteData(httpContext)?.Route as Route;
            return route == null ? string.Empty : route.ObfuscateUrlPath(httpContext.Request.Url.AbsolutePath.TrimStart('/'));
        }
    }
}