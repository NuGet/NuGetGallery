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
    public class VersionRouteConstraint : IRouteConstraint
    {
        public bool Match(HttpContextBase httpContext, Route route, string parameterName, RouteValueDictionary values, RouteDirection routeDirection)
        {
            if (routeDirection == RouteDirection.UrlGeneration)
            {
                return true;
            }

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
            
            if (versionText.Equals(LatestPackageRouteVerifier.SupportedRoutes.AbsoluteLatestUrlString, StringComparison.InvariantCultureIgnoreCase))
            {
                return true;
            }

            return NuGetVersion.TryParse(versionText, out _);
        }
    }
}
