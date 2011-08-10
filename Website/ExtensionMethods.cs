using System;
using System.Collections.Generic;
using System.Data.Services;
using System.Linq;
using System.ServiceModel.Activation;
using System.Web.Routing;
using System.Web.WebPages;

namespace NuGetGallery {
    public static class ExtensionMethods {
        public static void MapServiceRoute(
            this RouteCollection routes,
            string routeName,
            string routeUrl,
            Type serviceType) {
            var serviceRoute = new ServiceRoute(routeUrl, new DataServiceHostFactory(), serviceType);
            serviceRoute.Defaults = new RouteValueDictionary { { "serviceType", "odata" } };
            serviceRoute.Constraints = new RouteValueDictionary { { "serviceType", "odata" } };
            routes.Add(routeName, serviceRoute);
        }

        public static string Flatten(this ICollection<PackageAuthor> authors) {
            return string.Join(",", authors.Select(a => a.Name).ToArray());
        }

        public static string Flatten(this ICollection<PackageDependency> dependencies) {
            return string.Join("|", dependencies.Select(d => string.Format("{0}:{1}", d.Id, d.VersionRange)).ToArray());
        }

        public static HelperResult Flatten(this IEnumerable<string> items, Func<string, HelperResult> template) {
            var formattedItems = items.Select(item => template(item).ToHtmlString());

            return new HelperResult(writer => {
                writer.Write(string.Join(", ", formattedItems.ToArray()));
            });
        }
    }
}