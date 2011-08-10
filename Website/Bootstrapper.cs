using System.Web.Mvc;
using System.Web.Routing;

[assembly: WebActivator.PreApplicationStartMethod(typeof(NuGetGallery.Bootstrapper), "Start")]

namespace NuGetGallery {
    public static class Bootstrapper {
        public static void Start() {
            RegisterRoutes(RouteTable.Routes);

            // TODO: move profile bootstrapping and container bootstrapping to here
        }

        public static void RegisterRoutes(RouteCollection routes) {
            // TODO: add route tests
            routes.MapRoute(
                RouteName.Home,
                "",
                new { controller = PagesController.Name, action = ActionName.Home });

            routes.MapRoute(
                RouteName.Register,
                "Users/Account/Register",
                new { controller = UsersController.Name, action = ActionName.Register });

            routes.MapRoute(
                RouteName.SignIn,
                "Users/Account/LogOn",
                new { controller = AuthenticationController.Name, action = ActionName.SignIn });

            routes.MapRoute(
                RouteName.SignOut,
                "Users/Account/LogOff",
                new { controller = AuthenticationController.Name, action = ActionName.SignOut });

            routes.MapRoute(
                RouteName.SubmitPackage,
                "Contribute/NewSubmission",
                new { controller = PackagesController.Name, action = ActionName.SubmitPackage });

            routes.MapRoute(
                RouteName.ReportAbuse,
                "Package/ReportAbuse/{id}/{version}",
                new { controller = PackagesController.Name, action = ActionName.ReportAbuse });

            routes.MapRoute(
                RouteName.ContactOwners,
                "Package/ContactOwners/{id}",
                new { controller = PackagesController.Name, action = ActionName.ContactOwners });

            routes.MapRoute(
                RouteName.VerifyPackage,
                "Package/New/{id}/{version}",
                new { controller = PackagesController.Name, action = ActionName.VerifyPackage });

            routes.MapRoute(
                RouteName.DisplayPackage,
                "List/Packages/{id}/{version}",
                new { controller = PackagesController.Name, action = ActionName.DisplayPackage, version = UrlParameter.Optional });

            routes.MapRoute(
                RouteName.Contribute,
                "Contribute/Index",
                new { controller = PagesController.Name, action = ActionName.Contribute });

            routes.MapRoute(
                RouteName.ListPackages,
                "List/Packages",
                new { controller = PackagesController.Name, action = ActionName.ListPackages });

            routes.MapRoute(
                RouteName.Search,
                "Search",
                new { controller = "Search", action = "Results" });

            routes.MapServiceRoute(
                RouteName.ApiFeeds,
                "api/feeds",
                typeof(Feeds));
        }
    }
}