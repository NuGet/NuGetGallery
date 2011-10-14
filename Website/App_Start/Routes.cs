using System.Web.Mvc;
using System.Web.Routing;
using MvcHaack.Ajax;
using RouteMagic;

namespace NuGetGallery
{
    public static class Routes
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.MapRoute(
                RouteName.Home,
                "",
                MVC.Pages.Home());

            routes.Add(new JsonRoute("json/{controller}"));

            routes.MapRoute(
                RouteName.Policies,
                "policies/{action}",
                MVC.Pages.Terms());

            var packageListRoute = routes.MapRoute(
                RouteName.ListPackages,
                "packages",
                MVC.Packages.ListPackages());

            routes.MapRoute(
                RouteName.UploadPackage,
                "upload/package",
                MVC.Packages.UploadPackage());

            // We need the following two routes (rather than just one) due to Routing's 
            // Consecutive Optional Parameter bug. :(
            var packageDisplayRoute = routes.MapRoute(
                RouteName.DisplayPackage,
                "packages/{id}/{version}",
                MVC.Packages.DisplayPackage().AddRouteValue("version", UrlParameter.Optional),
                null /*defaults*/,
                new { version = new VersionRouteConstraint() });

            var packageVersionActionRoute = routes.MapRoute(
                RouteName.PackageVersionAction,
                "packages/{id}/{version}/{action}",
                new { controller = MVC.Packages.Name },
                new { version = new VersionRouteConstraint() });

            var packageActionRoute = routes.MapRoute(
                RouteName.PackageAction,
                "packages/{id}/{action}",
                new { controller = MVC.Packages.Name });

            routes.MapRoute(
                RouteName.Authentication,
                "users/account/{action}",
                new { controller = MVC.Authentication.Name });

            routes.MapRoute(
                RouteName.Profile,
                "profiles/{username}",
                MVC.Users.Profiles());

            routes.MapRoute(
                RouteName.PasswordReset,
                "account/{action}/{username}/{token}",
                MVC.Users.ResetPassword());

            routes.MapRoute(
                RouteName.Account,
                "account/{action}",
                MVC.Users.Account());

            routes.MapRoute(
                RouteName.PushPackageApi,
                "PackageFiles/{apiKey}/nupkg",
                MVC.Api.CreatePackage());

            routes.MapRoute(
                RouteName.PublishPackageApi,
                "PublishedPackages/Publish",
                MVC.Api.PublishPackage());

            routes.MapRoute(
                RouteName.DeletePackageApi,
                "Packages/{apiKey}/{id}/{version}",
                MVC.Api.DeletePackage());

            routes.MapServiceRoute(
                RouteName.V1ApiFeed,
                "api/feeds/v1",
                typeof(V1Feed));

            routes.MapServiceRoute(
                RouteName.V2ApiFeed,
                "api/feeds/v2",
                typeof(V2Feed));

            routes.MapServiceRoute(
                RouteName.ApiFeed,
                "api/feeds",
                typeof(V2Feed));

            // Redirected Legacy Routes

            routes.Redirect(
                r => r.MapRoute(
                    "ReportAbuse",
                    "Package/ReportAbuse/{id}/{version}",
                    MVC.Packages.ReportAbuse()),
                permanent: true).To(packageVersionActionRoute);

            routes.Redirect(
                r => r.MapRoute(
                    "PackageActions",
                    "Package/{action}/{id}",
                    MVC.Packages.ContactOwners(),
                    null /*defaults*/,
                    // This next bit looks bad, but it's not. It will never change because 
                    // it's mapping the legacy routes to the new better routes.
                    new { action = "ContactOwners|ManagePackageOwners" }),
                permanent: true).To(packageActionRoute);

            routes.Redirect(
                r => r.MapRoute(
                    "PublishPackage",
                    "Package/New/{id}/{version}",
                    MVC.Packages.PublishPackage()),
                permanent: true).To(packageVersionActionRoute);

            // TODO: this route looks broken as there is no EditPackage action
            //routes.Redirect(
            //    r => r.MapRoute(
            //        "EditPackage",
            //        "Package/Edit/{id}/{version}",
            //        new { controller = PackagesController.Name, action = "EditPackage" }),
            //    permanent: true).To(packageVersionActionRoute);

            routes.Redirect(
                r => r.MapRoute(
                    RouteName.ListPackages,
                    "List/Packages",
                    MVC.Packages.ListPackages()),
                permanent: true).To(packageListRoute);

            routes.Redirect(
                r => r.MapRoute(
                    RouteName.DisplayPackage,
                    "List/Packages/{id}/{version}",
                    MVC.Packages.DisplayPackage().AddRouteValue("version", UrlParameter.Optional)),
                permanent: true).To(packageDisplayRoute);
        }
    }
}