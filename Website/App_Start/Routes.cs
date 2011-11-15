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

            var uploadPackageRoute = routes.MapRoute(
                RouteName.UploadPackage,
                "packages/upload",
                MVC.Packages.UploadPackage());

            routes.MapRoute(
                RouteName.VerifyPackage,
                "packages/verify-upload",
                MVC.Packages.VerifyPackage());

            routes.MapRoute(
                RouteName.PackageOwnerConfirmation,
                "packages/{id}/owners/{username}/confirm/{token}",
                new { controller = MVC.Packages.Name, action = "ConfirmOwner" });

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
                RouteName.DownloadPackage,
                "api/v2/package/{id}/{version}",
                MVC.Api.GetPackage(),
                defaults: null,
                constraints: new { httpMethod = new HttpMethodConstraint("GET") });

            routes.MapRoute(
                "legacy-" + RouteName.PushPackageApi,
                "PackageFiles/{apiKey}/nupkg",
                MVC.Api.CreatePackage());

            routes.MapRoute(
                RouteName.PushPackageApi,
                "api/v2/package",
                MVC.Api.CreatePackage(),
                defaults: null,
                constraints: new { httpMethod = new HttpMethodConstraint("POST") });

            routes.MapRoute(
                RouteName.PublishPackageApi,
                "PublishedPackages/Publish",
                MVC.Api.PublishPackage());

            routes.MapRoute(
                "legacy-" + RouteName.DeletePackageApi,
                "Packages/{apiKey}/{id}/{version}",
                MVC.Api.DeletePackage());

            routes.MapRoute(
                RouteName.DeletePackageApi,
                "api/v2/package/{id}/{version}",
                MVC.Api.DeletePackage(),
                defaults: null,
                constraints: new { httpMethod = new HttpMethodConstraint("DELETE") });

            routes.MapServiceRoute(
                RouteName.V1ApiFeed,
                "api/v1",
                typeof(V1Feed));

            routes.MapServiceRoute(
                "legacy-" + RouteName.V1ApiFeed,
                "v1/FeedService.svc",
                typeof(V1Feed));

            routes.MapServiceRoute(
                RouteName.V2ApiFeed,
                "api/v2/",
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

            routes.Redirect(
                r => r.MapRoute(
                    RouteName.NewSubmission,
                    "Contribute/NewSubmission",
                    MVC.Packages.UploadPackage()),
                permanent: true).To(uploadPackageRoute);
        }
    }
}