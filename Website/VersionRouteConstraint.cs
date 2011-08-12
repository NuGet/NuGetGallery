using System;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace NuGetGallery {
    public class VersionRouteConstraint : IRouteConstraint {
        public bool Match(HttpContextBase httpContext, Route route, string parameterName, RouteValueDictionary values, RouteDirection routeDirection) {
            object versionValue;
            if (!values.TryGetValue(parameterName, out versionValue)) {
                return true;
            }

            if (versionValue == UrlParameter.Optional) {
                return true;
            }

            string versionText = versionValue.ToString();
            if (versionText == string.Empty) {
                return true;
            }
            Version ignored;
            return Version.TryParse(versionText, out ignored);
        }
    }
}