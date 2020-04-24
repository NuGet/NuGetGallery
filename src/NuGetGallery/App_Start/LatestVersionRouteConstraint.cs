// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using NuGet.Versioning;

namespace NuGetGallery
{
    public class LatestVersionRouteConstraint : IRouteConstraint
    {
        /// <inheritdoc />
        public bool Match(HttpContextBase httpContext, Route route, string parameterName, RouteValueDictionary values, RouteDirection routeDirection)
        {
            object versionValue;
            if (!values.TryGetValue(parameterName, out versionValue))
            {
                return true;
            }

            if (versionValue == null || versionValue == UrlParameter.Optional)
            {
                return true;
            }

            string versionText = versionValue.ToString();
            if (versionText.Length == 0)
            {
                return true;
            }
            
            if (versionText.Equals(GalleryConstants.AbsoluteLatestUrlString, StringComparison.InvariantCultureIgnoreCase))
            {
                return true;
            }

            if (route.Url.Equals(GalleryConstants.LatestUrlString, StringComparison.InvariantCultureIgnoreCase)
                || route.Url.Equals(GalleryConstants.LatestUrlWithPreleaseString, StringComparison.InvariantCultureIgnoreCase)
                || route.Url.Equals(GalleryConstants.LatestUrlWithPreleaseAndVersionString, StringComparison.InvariantCultureIgnoreCase))
                return true;

            NuGetVersion ignored;
            return NuGetVersion.TryParse(versionText, out ignored);
        }
    }
}