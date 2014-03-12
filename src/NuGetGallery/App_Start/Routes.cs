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
                new { controller = MVC.Pages.Name, action = "Home" }); // T4MVC doesn't work with Async Action

            routes.MapRoute(
                RouteName.Error500,
                "errors/500",
                MVC.Errors.InternalError());

            routes.MapRoute(
                RouteName.Error404,
                "errors/404",
                MVC.Errors.NotFound());

            routes.MapRoute(
                RouteName.StatisticsHome,
                "stats",
                new { controller = MVC.Statistics.Name, action = "Index" });

            routes.MapRoute(
                RouteName.Stats,
                "stats/totals",
                MVC.Statistics.Totals());

            routes.MapRoute(
                RouteName.StatisticsPackages,
                "stats/packages",
                new { controller = MVC.Statistics.Name, action = "Packages" });

            routes.MapRoute(
                RouteName.StatisticsPackageVersions,
                "stats/packageversions",
                new { controller = MVC.Statistics.Name, action = "PackageVersions" });

            routes.MapRoute(
                RouteName.StatisticsPackageDownloadsDetail,
                "stats/packages/{id}/{version}",
                new { controller = MVC.Statistics.Name, action = "PackageDownloadsDetail" });

            routes.MapRoute(
                RouteName.StatisticsPackageDownloadsByVersion,
                "stats/packages/{id}",
                new { controller = MVC.Statistics.Name, action = "PackageDownloadsByVersion" });
           
            routes.Add(new JsonRoute("json/{controller}"));

            routes.MapRoute(
                RouteName.Policies,
                "policies/{action}",
                new { controller = MVC.Pages.Name });

            routes.MapRoute(
                RouteName.Pages,
                "pages/{pageName}",
                new { controller = MVC.Pages.Name, action = "Page" });

            var packageListRoute = routes.MapRoute(
                RouteName.ListPackages,
                "packages",
                new { controller = MVC.Packages.Name, action = "ListPackages" });

            var uploadPackageRoute = routes.MapRoute(
                RouteName.UploadPackage,
                "packages/upload",
                new { controller = MVC.Packages.Name, action = "UploadPackage" });

            routes.MapRoute(
                RouteName.UploadPackageProgress,
                "packages/upload-progress",
                MVC.Packages.UploadPackageProgress());

            routes.MapRoute(
                RouteName.VerifyPackage,
                "packages/verify-upload",
                new { controller = MVC.Packages.Name, action = "VerifyPackage" });

            routes.MapRoute(
                RouteName.CancelUpload,
                "packages/cancel-upload",
                new { controller = MVC.Packages.Name, action = "CancelUpload"});

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

            routes.MapRoute(
                RouteName.PackageEnableLicenseReport,
                "packages/{id}/{version}/EnableLicenseReport",
                new { controller = MVC.Packages.Name, action = "SetLicenseReportVisibility", visible = true },
                new { version = new VersionRouteConstraint() });
            
            routes.MapRoute(
                RouteName.PackageDisableLicenseReport,
                "packages/{id}/{version}/DisableLicenseReport",
                new { controller = MVC.Packages.Name, action = "SetLicenseReportVisibility", visible = false },
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

            var confirmationRequiredRoute = routes.MapRoute(
                "ConfirmationRequired",
                "account/ConfirmationRequired",
                MVC.Users.ConfirmationRequired());

            //Redirecting v1 Confirmation Route
            routes.Redirect(
                r => r.MapRoute(
                    "v1Confirmation",
                    "Users/Account/ChallengeEmail")).To(confirmationRequiredRoute);

            routes.MapRoute(
                RouteName.ExternalAuthenticationCallback,
                "users/account/authenticate/return",
                new { controller = MVC.Authentication.Name, action = "LinkExternalAccount" });

            routes.MapRoute(
                RouteName.ExternalAuthentication,
                "users/account/authenticate/{provider}",
                new { controller = MVC.Authentication.Name, action = MVC.Authentication.ActionNames.Authenticate });

            routes.MapRoute(
                RouteName.Authentication,
                "users/account/{action}",
                new { controller = MVC.Authentication.Name });

            routes.MapRoute(
                RouteName.Profile,
                "profiles/{username}",
                MVC.Users.Profiles());

            routes.MapRoute(
                RouteName.LegacyRegister,
                "account/register",
                new { controller = MVC.Authentication.Name, action = "Register" });

            routes.MapRoute(
                RouteName.RemovePassword,
                "account/RemoveCredential/password",
                new { controller = MVC.Users.Name, action = "RemovePassword" });

            routes.MapRoute(
                RouteName.RemoveCredential,
                "account/RemoveCredential/{credentialType}",
                new { controller = MVC.Users.Name, action = "RemoveCredential" });

            routes.MapRoute(
                RouteName.PasswordReset,
                "account/forgotpassword/{username}/{token}",
                MVC.Users.ResetPassword(forgot: true));

            routes.MapRoute(
                RouteName.PasswordSet,
                "account/setpassword/{username}/{token}",
                MVC.Users.ResetPassword(forgot: false));

            routes.MapRoute(
                RouteName.ConfirmAccount,
                "account/confirm/{username}/{token}",
                new { controller = MVC.Users.Name, action = "Confirm" });

            routes.MapRoute(
                RouteName.SubscribeToEmails,
                "account/subscribe",
                MVC.Users.ChangeEmailSubscription(true));

            routes.MapRoute(
                RouteName.UnsubscribeFromEmails,
                "account/unsubscribe",
                MVC.Users.ChangeEmailSubscription(false));

            routes.MapRoute(
                RouteName.Account,
                "account/{action}",
                MVC.Users.Account());

            routes.MapRoute(
                RouteName.CuratedFeed,
                "curated-feeds/{name}",
                new { controller = MVC.CuratedFeeds.Name, action = "CuratedFeed" });

            routes.MapRoute(
                RouteName.CuratedFeedListPackages,
                "curated-feeds/{curatedFeedName}/packages",
                new { controller = MVC.CuratedFeeds.Name, action = "ListPackages" });

            routes.MapRoute(
                RouteName.CreateCuratedPackageForm,
                "forms/add-package-to-curated-feed",
                new { controller = MVC.CuratedPackages.Name, action = "CreateCuratedPackageForm" });

            routes.MapRoute(
                RouteName.CuratedPackage,
                "curated-feeds/{curatedFeedName}/curated-packages/{curatedPackageId}",
                new { controller = MVC.CuratedPackages.Name, action = "CuratedPackage" });

            routes.MapRoute(
                RouteName.CuratedPackages,
                "curated-feeds/{curatedFeedName}/curated-packages",
                new { controller = MVC.CuratedPackages.Name, action = "CuratedPackages" });

            // TODO : Most of the routes are essentially of the format api/v{x}/*. We should refactor the code to vary them by the version.
            // V1 Routes
            // If the push url is /api/v1 then NuGet.Core would ping the path to resolve redirection. 
            routes.MapRoute(
                "v1" + RouteName.VerifyPackageKey,
                "api/v1/verifykey/{id}/{version}",
                MVC.Api.VerifyPackageKey(),
                defaults: new { id = UrlParameter.Optional, version = UrlParameter.Optional });

            var downloadRoute = routes.MapRoute(
                "v1" + RouteName.DownloadPackage,
                "api/v1/package/{id}/{version}",
                defaults: new { controller = MVC.Api.Name, action = "GetPackageApi", version = UrlParameter.Optional },
                constraints: new { httpMethod = new HttpMethodConstraint("GET") });

            routes.MapRoute(
                "v1" + RouteName.PushPackageApi,
                "v1/PackageFiles/{apiKey}/nupkg",
                defaults: new { controller = MVC.Api.Name, action = "PushPackageApi" },
                constraints: new { httpMethod = new HttpMethodConstraint("POST") });

            routes.MapRoute(
                "v1" + RouteName.DeletePackageApi,
                "v1/Packages/{apiKey}/{id}/{version}",
                MVC.Api.DeletePackage());

            routes.MapRoute(
                "v1" + RouteName.PublishPackageApi,
                "v1/PublishedPackages/Publish",
                MVC.Api.PublishPackage());

            // V2 routes
            routes.MapRoute(
                "v2" + RouteName.VerifyPackageKey,
                "api/v2/verifykey/{id}/{version}",
                MVC.Api.VerifyPackageKey(),
                defaults: new { id = UrlParameter.Optional, version = UrlParameter.Optional });

            routes.MapRoute(
                "v2CuratedFeeds" + RouteName.DownloadPackage,
                "api/v2/curated-feeds/package/{id}/{version}",
                defaults: new { controller = MVC.Api.Name, action = "GetPackageApi", version = UrlParameter.Optional },
                constraints: new { httpMethod = new HttpMethodConstraint("GET") });

            routes.MapRoute(
                "v2" + RouteName.DownloadPackage,
                "api/v2/package/{id}/{version}",
                defaults: new { controller = MVC.Api.Name, action = "GetPackageApi", version = UrlParameter.Optional },
                constraints: new { httpMethod = new HttpMethodConstraint("GET") });

            routes.MapRoute(
                "v2" + RouteName.PushPackageApi,
                "api/v2/package",
                defaults: new { controller = MVC.Api.Name, action = "PushPackageApi" },
                constraints: new { httpMethod = new HttpMethodConstraint("PUT") });

            routes.MapRoute(
                "v2" + RouteName.DeletePackageApi,
                "api/v2/package/{id}/{version}",
                MVC.Api.DeletePackage(),
                defaults: null,
                constraints: new { httpMethod = new HttpMethodConstraint("DELETE") });

            routes.MapRoute(
                "v2" + RouteName.PublishPackageApi,
                "api/v2/package/{id}/{version}",
                MVC.Api.PublishPackage(),
                defaults: null,
                constraints: new { httpMethod = new HttpMethodConstraint("POST") });

            routes.MapRoute(
                "v2PackageIds",
                "api/v2/package-ids",
                MVC.Api.GetPackageIds());

            routes.MapRoute(
                "v2PackageVersions",
                "api/v2/package-versions/{id}",
                MVC.Api.GetPackageVersions());

            routes.MapRoute(
                RouteName.StatisticsDownloadsApi,
                "api/v2/stats/downloads/last6weeks",
                defaults: new { controller = MVC.Api.Name, action = "StatisticsDownloadsApi" },
                constraints: new { httpMethod = new HttpMethodConstraint("GET") });

            routes.MapRoute(
                RouteName.ServiceAlert,
                "api/v2/service-alert",
                defaults: new { controller = MVC.Api.Name, action = "ServiceAlert" },
                constraints: new { httpMethod = new HttpMethodConstraint("GET") });

            routes.MapRoute(
                RouteName.TypeaheadApi,
                "api/v2/typeahead",
                defaults: new { controller = MVC.Api.Name, action = "GetTypeaheadApi" },
                constraints: new { httpMethod = new HttpMethodConstraint("GET") });

            routes.MapRoute(
                RouteName.DownloadNuGetExe,
                "nuget.exe",
                new { controller = MVC.Api.Name, action = "GetNuGetExeApi" });

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
                    RouteName.ListPackages,
                    "List/Packages",
                    new { controller = MVC.Packages.Name, action = "ListPackages" }),
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
                    new { controller = MVC.Packages.Name, action = "UploadPackage" }),
                permanent: true).To(uploadPackageRoute);

            routes.Redirect(
                r => r.MapRoute(
                    "LegacyDownloadRoute",
                    "v1/Package/Download/{id}/{version}",
                    new { controller = MVC.Api.Name, action = "GetPackageApi", version = UrlParameter.Optional }),
                permanent: true).To(downloadRoute);
        }

        // note: Pulled out service route registration separately because it's not testable T.T (won't run outside IIS/WAS) 
        public static void RegisterServiceRoutes(RouteCollection routes)
        {
            routes.MapServiceRoute(
                RouteName.V1ApiFeed,
                "api/v1/FeedService.svc",
                typeof(V1Feed));

            routes.MapServiceRoute(
                "LegacyFeedService",
                "v1/FeedService.svc",
                typeof(V1Feed));

            routes.MapServiceRoute(
                "v1" + RouteName.V1ApiFeed,
                "api/v1",
                typeof(V1Feed));

            routes.MapServiceRoute(
                RouteName.V2ApiCuratedFeed,
                "api/v2/curated-feed",
                typeof(V2CuratedFeed));

            routes.MapServiceRoute(
                RouteName.V2ApiFeed,
                "api/v2/",
                typeof(V2Feed));
        }
    }
}