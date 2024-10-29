// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using NuGet.Versioning;
using NuGetGallery.Services.Helpers;

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

            if (route.Url.Equals(LatestPackageRouteVerifier.SupportedRoutes.LatestUrlString, StringComparison.InvariantCultureIgnoreCase)
                || route.Url.Equals(LatestPackageRouteVerifier.SupportedRoutes.LatestUrlWithPreleaseString, StringComparison.InvariantCultureIgnoreCase)
                || route.Url.Equals(LatestPackageRouteVerifier.SupportedRoutes.LatestUrlWithPreleaseAndVersionString, StringComparison.InvariantCultureIgnoreCase))
                return true;

            return NuGetVersion.TryParse(versionText, out _);
        }
    }
}