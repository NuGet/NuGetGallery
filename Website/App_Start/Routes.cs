using System.Web.Mvc;
using System.Web.Routing;
using RouteMagic;

namespace NuGetGallery {
    public static class Routes {
        public static void RegisterRoutes(RouteCollection routes) {
            routes.MapRoute(
                RouteName.Home,
                "",
                new { controller = PagesController.Name, action = "Home" });

            var packageListRoute = routes.MapRoute(
                RouteName.ListPackages,
                "packages",
                new { controller = PackagesController.Name, action = "ListPackages" });

            // We need the following two routes (rather than just one) due to Routing's 
            // Consecutive Optional Parameter bug. :(
            var packageDisplayRoute = routes.MapRoute(
                RouteName.DisplayPackage,
                "packages/{id}/{version}",
                new { controller = PackagesController.Name, action = "DisplayPackage", version = UrlParameter.Optional },
                new { version = new VersionRouteConstraint() });

            var packageVersionActionRoute = routes.MapRoute(
                RouteName.PackageVersionAction,
                "packages/{id}/{version}/{action}",
                new { controller = PackagesController.Name },
                new { version = new VersionRouteConstraint() });

            var packageActionRoute = routes.MapRoute(
                RouteName.PackageAction,
                "packages/{id}/{action}",
                new { controller = PackagesController.Name });

            routes.MapRoute(
                RouteName.Authentication,
                "Users/Account/{action}",
                new { controller = AuthenticationController.Name });

            routes.MapRoute(
                RouteName.UploadPackage,
                "upload/package",
                new { controller = PackagesController.Name, action = "UploadPackage" });

            routes.MapRoute(
                RouteName.Account,
                "account/{action}",
                new { controller = UsersController.Name, action = "Account" });

            routes.MapRoute(
                RouteName.PushPackageApi,
                "PackageFiles/{apiKey}/nupkg",
                new { controller = ApiController.Name, action = "PushPackageApi" });

            routes.MapRoute(
                RouteName.PublishPackageApi,
                "PublishedPackages/Publish",
                new { controller = ApiController.Name, action = "PublishPackageApi" });

            routes.MapRoute(
                RouteName.DeletePackageApi,
                "Packages/{apiKey}/{id}/{version}",
                new { controller = ApiController.Name, action = "DeletePackageApi" });

            routes.MapServiceRoute(
                RouteName.ApiFeeds,
                "api/feeds",
                typeof(Feeds));

            // Redirected Legacy Routes

            routes.Redirect(
                r => r.MapRoute(
                    "ReportAbuse",
                    "Package/ReportAbuse/{id}/{version}",
                    new { controller = PackagesController.Name, action = "ReportAbuse" }),
                permanent: true).To(packageVersionActionRoute);

            routes.Redirect(
                r => r.MapRoute(
                    "PackageActions",
                    "Package/{action}/{id}",
                    new { controller = PackagesController.Name, action = "ContactOwners" },
                    // This next bit looks bad, but it's not. It will never change because 
                    // it's mapping the legacy routes to the new better routes.
                    new { action = "ContactOwners|ManagePackageOwners" }),
                permanent: true).To(packageActionRoute);

            routes.Redirect(
                r => r.MapRoute(
                    "PublishPackage",
                    "Package/New/{id}/{version}",
                    new { controller = PackagesController.Name, action = "PublishPackage" }),
                permanent: true).To(packageVersionActionRoute);

            routes.Redirect(
                r => r.MapRoute(
                    "EditPackage",
                    "Package/Edit/{id}/{version}",
                    new { controller = PackagesController.Name, action = "EditPackage" }),
                permanent: true).To(packageVersionActionRoute);

            routes.Redirect(
                r => r.MapRoute(
                    RouteName.ListPackages,
                    "List/Packages",
                    new { controller = PackagesController.Name, action = "ListPackages" }),
                permanent: true).To(packageListRoute);

            routes.Redirect(
                r => r.MapRoute(
                    RouteName.DisplayPackage,
                    "List/Packages/{id}/{version}",
                    new { controller = PackagesController.Name, action = "DisplayPackage", version = UrlParameter.Optional }),
                permanent: true).To(packageDisplayRoute);
        }
    }
}