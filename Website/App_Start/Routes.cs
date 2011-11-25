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
                RouteName.CancelUpload,
                "packages/cancel-upload",
                MVC.Packages.CancelUpload());

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

            // V1 Routes
            // If the push url is /api/v1 then NuGet.Core would ping the path to resolve redirection. 
            routes.MapDelegate(
                "v1" + RouteName.ApiPingAction,
                "api/v1",
                request => new EmptyResult());

            routes.MapServiceRoute(
                RouteName.V1ApiFeed,
                "api/v1/FeedService.svc",
                typeof(V1Feed));

            routes.MapRoute(
                "v1" + RouteName.PushPackageApi,
                "api/v1/PackageFiles/{apiKey}/nupkg",
                MVC.Api.CreatePackagePost());

            routes.MapRoute(
                "v1" + RouteName.DeletePackageApi,
                "api/v1/Packages/{apiKey}/{id}/{version}",
                MVC.Api.DeletePackage());

            routes.MapRoute(
                "v1" + RouteName.PublishPackageApi,
                "api/v1/PublishedPackages/Publish",
                MVC.Api.PublishPackage());

            routes.MapServiceRoute(
                "v1" + RouteName.V1ApiFeed,
                "api/v1",
                typeof(V1Feed));

            // V2 routes
            routes.MapDelegate(
                "v2" + RouteName.ApiPingAction,
                "api/v2",
                request => new EmptyResult());

            routes.MapRoute(
                "v2" + RouteName.DownloadPackage,
                "api/v2/package/{id}/{version}",
                MVC.Api.GetPackage(),
                defaults: new { version = UrlParameter.Optional },
                constraints: new { httpMethod = new HttpMethodConstraint("GET") });

            routes.MapRoute(
                "v2" + RouteName.PushPackageApi,
                "api/v2/package",
                MVC.Api.CreatePackagePut(),
                defaults: null,
                constraints: new { httpMethod = new HttpMethodConstraint("PUT") });

            routes.MapRoute(
                "v2" + RouteName.DeletePackageApi,
                "api/v2/package/{id}/{version}",
                MVC.Api.DeletePackage(),
                defaults: null,
                constraints: new { httpMethod = new HttpMethodConstraint("DELETE") });

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