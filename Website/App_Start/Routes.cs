using System.Web.Mvc;
using System.Web.Routing;
using RouteMagic;

namespace NuGetGallery {
    public static class Routes {
        public static void RegisterRoutes(RouteCollection routes) {
            routes.MapRoute(
                RouteName.Home,
                "",
                new { controller = PagesController.Name, action = ActionName.Home });

            var packageListRoute = routes.MapRoute(
                RouteName.ListPackages,
                "packages",
                new { controller = PackagesController.Name, action = ActionName.ListPackages });

            // We need the following two routes (rather than just one) due to Routing's 
            // Consecutive Optional Parameter bug. :(
            var packageDisplayRoute = routes.MapRoute(
                RouteName.DisplayPackage,
                "packages/{id}/{version}",
                new { controller = PackagesController.Name, action = ActionName.DisplayPackage, version = UrlParameter.Optional },
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
                RouteName.Register,
                "Users/Account/Register",
                new { controller = UsersController.Name, action = ActionName.Register });

            routes.MapRoute(
                RouteName.Authentication,
                "Users/Account/{action}",
                new { controller = AuthenticationController.Name });

            routes.MapRoute(
                RouteName.UploadPackage,
                "upload/package",
                new { controller = PackagesController.Name, action = ActionName.UploadPackage });

            routes.MapRoute(
                RouteName.Account,
                "account",
                new { controller = UsersController.Name, action = ActionName.Account });

            routes.MapRoute(
                RouteName.PushPackageApi,
                "PackageFiles/{apiKey}/nupkg",
                new { controller = ApiController.Name, action = ActionName.PushPackageApi });

            routes.MapRoute(
                RouteName.PublishPackageApi,
                "PublishedPackages/Publish",
                new { controller = ApiController.Name, action = ActionName.PublishPackageApi });

            routes.MapRoute(
                RouteName.DeletePackageApi,
                "Packages/{apiKey}/{id}/{version}",
                new { controller = ApiController.Name, action = ActionName.DeletePackageApi });

            routes.MapServiceRoute(
                RouteName.ApiFeeds,
                "api/feeds",
                typeof(Feeds));

            // Redirected Legacy Routes

            routes.Redirect(
                r => r.MapRoute(
                    "ReportAbuse",
                    "Package/ReportAbuse/{id}/{version}",
                    new { controller = PackagesController.Name, action = ActionName.ReportAbuse }),
                permanent: true).To(packageVersionActionRoute);

            routes.Redirect(
                r => r.MapRoute(
                    "PackageActions",
                    "Package/{action}/{id}",
                    new { controller = PackagesController.Name, action = ActionName.ContactOwners },
                    new { action = ActionName.ContactOwners + "|" + ActionName.ManagePackageOwners }),
                permanent: true).To(packageActionRoute);

            routes.Redirect(
                r => r.MapRoute(
                    "PublishPackage",
                    "Package/New/{id}/{version}",
                    new { controller = PackagesController.Name, action = ActionName.PublishPackage }),
                permanent: true).To(packageVersionActionRoute);

            routes.Redirect(
                r => r.MapRoute(
                    "EditPackage",
                    "Package/Edit/{id}/{version}",
                    new { controller = PackagesController.Name, action = ActionName.EditPackage }),
                permanent: true).To(packageVersionActionRoute);

            routes.Redirect(
                r => r.MapRoute(
                    RouteName.ListPackages,
                    "List/Packages",
                    new { controller = PackagesController.Name, action = ActionName.ListPackages }),
                permanent: true).To(packageListRoute);

            routes.Redirect(
                r => r.MapRoute(
                    RouteName.DisplayPackage,
                    "List/Packages/{id}/{version}",
                    new { controller = PackagesController.Name, action = ActionName.DisplayPackage, version = UrlParameter.Optional }),
                permanent: true).To(packageDisplayRoute);
        }
    }
}