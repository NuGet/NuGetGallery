using System;
using System.Web.Routing;

namespace NuGetGallery.Services.Helpers
{
    public static class LatestPackageRouteVerifier
    {
        public class SupportedRoutes
        {
            public const string LatestUrlString = "packages/{id}/latest";
            public const string LatestUrlWithPreleaseString = "packages/{id}/latest/prerelease";
            public const string LatestUrlWithPreleaseAndVersionString = "packages/{id}/latest/prerelease/{version}";
            public const string AbsoluteLatestUrlString = "absoluteLatest";
        }
        
        public static bool IsLatestRoute(RouteBase route, out bool preRelease)
        {
            preRelease = false;
            if (route is Route r)
            {
                if (r.Url.Equals(SupportedRoutes.LatestUrlString, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
                if (r.Url.Equals(SupportedRoutes.LatestUrlWithPreleaseString, StringComparison.OrdinalIgnoreCase))
                {
                    preRelease = true;
                    return true;
                }
                if (r.Url.Equals(SupportedRoutes.LatestUrlWithPreleaseAndVersionString, StringComparison.OrdinalIgnoreCase))
                {
                    preRelease = true;
                    return true;
                }
            }

            return false;
        }
    }
}