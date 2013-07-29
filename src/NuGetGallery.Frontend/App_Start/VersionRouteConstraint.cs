using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using NuGet;

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
            SemanticVersion ignored;
            return SemanticVersion.TryParse(versionText, out ignored);
        }
    }
}