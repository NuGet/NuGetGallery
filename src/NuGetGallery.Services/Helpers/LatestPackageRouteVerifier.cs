// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Web.Routing;

namespace NuGetGallery.Services.Helpers
{
    public static class LatestPackageRouteVerifier
    {
        public static class SupportedRoutes
        {
            public const string LatestUrlString = "packages/{id}/latest";
            public const string LatestUrlWithPreleaseString = "packages/{id}/latest/prerelease";
            public const string LatestUrlWithPreleaseAndVersionString = "packages/{id}/latest/prerelease/{version}";
            public const string AbsoluteLatestUrlString = "absoluteLatest";
        }
        
        public static bool IsLatestRoute(string routeUrl, out bool prerelease)
        {
            prerelease = false;
            
            if (routeUrl == null)
                return false;
            
            if (routeUrl.Equals(SupportedRoutes.LatestUrlString, StringComparison.InvariantCultureIgnoreCase))
            {
                return true;
            }
            if (routeUrl.Equals(SupportedRoutes.LatestUrlWithPreleaseString, StringComparison.InvariantCultureIgnoreCase))
            {
                prerelease = true;
                return true;
            }
            if (routeUrl.Equals(SupportedRoutes.LatestUrlWithPreleaseAndVersionString, StringComparison.InvariantCultureIgnoreCase))
            {
                prerelease = true;
                return true;
            }

            return false;
        }
    }
}