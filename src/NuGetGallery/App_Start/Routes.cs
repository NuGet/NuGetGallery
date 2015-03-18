using System.Web.Mvc;
using System.Web.Routing;
using MvcHaack.Ajax;
using RouteMagic;

namespace NuGetGallery
{
    public static class Routes
    {
        public static void RegisterRoutes(RouteCollection routes, bool feedOnlyMode = false)
        {
            if (!feedOnlyMode)
            {
                Routes.RegisterUIRoutes(routes);
            }
            else
            {
                // The home route is used as a probe path by Azure Load Balancer
                // to determine if the node is up. So, always register the home route
                // Just do so with an Empty Home, in the FeedOnlyMode, which simply returns a 200
                RouteTable.Routes.MapRoute(
                RouteName.Home,
                "",
                new { controller = "Pages", action = "EmptyHome" });
            }
            Routes.RegisterApiV2Routes(routes);
        }

        public static void RegisterUIRoutes(RouteCollection routes)
        {
            routes.MapRoute(
                RouteName.Home,
                "",
                new { controller = "Pages", action = "Home" }); // T4MVC doesn't work with Async Action

            routes.MapRoute(
                RouteName.Error500,
                "errors/500",
                new { controller = "Errors", action = "InternalError" });

            routes.MapRoute(
                RouteName.Error404,
                "errors/404",
                new { controller = "Errors", action = "NotFound" });

            routes.MapRoute(
                RouteName.StatisticsHome,
                "stats",
                new { controller = "Statistics", action = "Index" });

            routes.MapRoute(
                RouteName.Stats,
                "stats/totals",
                new { controller = "Statistics", action = "Totals" });

            routes.MapRoute(
                RouteName.StatisticsPackages,
                "stats/packages",
                new { controller = "Statistics", action = "Packages" });

            routes.MapRoute(
                RouteName.StatisticsPackageVersions,
                "stats/packageversions",
                new { controller = "Statistics", action = "PackageVersions" });

            routes.MapRoute(
                RouteName.StatisticsPackageDownloadsDetail,
                "stats/packages/{id}/{version}",
                new { controller = "Statistics", action = "PackageDownloadsDetail" });

            routes.MapRoute(
                RouteName.StatisticsPackageDownloadsByVersion,
                "stats/packages/{id}",
                new { controller = "Statistics", action = "PackageDownloadsByVersion" });
           
            routes.Add(new JsonRoute("json/{controller}"));

            routes.MapRoute(
                RouteName.Contributors,
                "pages/contributors",
                new { controller = "Pages", action = "Contributors" });

            routes.MapRoute(
                RouteName.Policies,
                "policies/{action}",
                new { controller = "Pages" });

            routes.MapRoute(
                RouteName.Pages,
                "pages/{pageName}",
                new { controller = "Pages", action = "Page" });

            var packageListRoute = routes.MapRoute(
                RouteName.ListPackages,
                "packages",
                new { controller = "Packages", action = "ListPackages" });

            var uploadPackageRoute = routes.MapRoute(
                RouteName.UploadPackage,
                "packages/upload",
                new { controller = "Packages", action = "UploadPackage" });

            routes.MapRoute(
                RouteName.UploadPackageProgress,
                "packages/upload-progress",
                new { controller = "Packages", action = "UploadPackageProgress" });

            routes.MapRoute(
                RouteName.VerifyPackage,
                "packages/verify-upload",
                new { controller = "Packages", action = "VerifyPackage" });

            routes.MapRoute(
                RouteName.CancelUpload,
                "packages/cancel-upload",
                new { controller = "Packages", action = "CancelUpload"});

            routes.MapRoute(
                RouteName.PackageOwnerConfirmation,
                "packages/{id}/owners/{username}/confirm/{token}",
                new { controller = "Packages", action = "ConfirmOwner" });

            // We need the following two routes (rather than just one) due to Routing's 
            // Consecutive Optional Parameter bug. :(
            var packageDisplayRoute = routes.MapRoute(
                RouteName.DisplayPackage,
                "packages/{id}/{version}",
                new { 
                    controller = "packages", 
                    action = "DisplayPackage", 
                    version = UrlParameter.Optional 
                },
                new { version = new VersionRouteConstraint() });

            routes.MapRoute(
                RouteName.PackageEnableLicenseReport,
                "packages/{id}/{version}/EnableLicenseReport",
                new { controller = "Packages", action = "SetLicenseReportVisibility", visible = true },
                new { version = new VersionRouteConstraint() });
            
            routes.MapRoute(
                RouteName.PackageDisableLicenseReport,
                "packages/{id}/{version}/DisableLicenseReport",
                new { controller = "Packages", action = "SetLicenseReportVisibility", visible = false },
                new { version = new VersionRouteConstraint() });

            var packageVersionActionRoute = routes.MapRoute(
                RouteName.PackageVersionAction,
                "packages/{id}/{version}/{action}",
                new { controller = "Packages" },
                new { version = new VersionRouteConstraint() });

            var packageActionRoute = routes.MapRoute(
                RouteName.PackageAction,
                "packages/{id}/{action}",
                new { controller = "Packages" });

            var confirmationRequiredRoute = routes.MapRoute(
                "ConfirmationRequired",
                "account/ConfirmationRequired",
                new { controller = "Users", action = "ConfirmationRequired" });

            //Redirecting v1 Confirmation Route
            routes.Redirect(
                r => r.MapRoute(
                    "v1Confirmation",
                    "Users/Account/ChallengeEmail")).To(confirmationRequiredRoute);

            routes.MapRoute(
                RouteName.ExternalAuthenticationCallback,
                "users/account/authenticate/return",
                new { controller = "Authentication", action = "LinkExternalAccount" });

            routes.MapRoute(
                RouteName.ExternalAuthentication,
                "users/account/authenticate/{provider}",
                new { controller = "Authentication", action = "Authenticate" });

            routes.MapRoute(
                RouteName.Authentication,
                "users/account/{action}",
                new { controller = "Authentication" });

            routes.MapRoute(
                RouteName.Profile,
                "profiles/{username}",
                new { controller = "Users", action = "Profiles" });

            routes.MapRoute(
                RouteName.LegacyRegister,
                "account/register",
                new { controller = "Authentication", action = "Register" });

            routes.MapRoute(
                RouteName.RemovePassword,
                "account/RemoveCredential/password",
                new { controller = "Users", action = "RemovePassword" });

            routes.MapRoute(
                RouteName.RemoveCredential,
                "account/RemoveCredential/{credentialType}",
                new { controller = "Users", action = "RemoveCredential" });

            routes.MapRoute(
                RouteName.PasswordReset,
                "account/forgotpassword/{username}/{token}",
                new { controller = "Users", action = "ResetPassword", forgot = true });

            routes.MapRoute(
                RouteName.PasswordSet,
                "account/setpassword/{username}/{token}",
                new { controller = "Users", action = "ResetPassword", forgot = false });

            routes.MapRoute(
                RouteName.ConfirmAccount,
                "account/confirm/{username}/{token}",
                new { controller = "Users", action = "Confirm" });

            routes.MapRoute(
                RouteName.SubscribeToEmails,
                "account/subscribe",
                new { controller = "Users", action = "ChangeEmailSubscription", subscribe = true });

            routes.MapRoute(
                RouteName.UnsubscribeFromEmails,
                "account/unsubscribe",
                new { controller = "Users", action = "ChangeEmailSubscription", subscribe = false });

            routes.MapRoute(
                RouteName.Account,
                "account/{action}",
                new { controller = "Users", action = "Account" });

            routes.MapRoute(
                RouteName.CuratedFeed,
                "curated-feeds/{name}",
                new { controller = "CuratedFeeds", action = "CuratedFeed" });

            routes.MapRoute(
                RouteName.CuratedFeedListPackages,
                "curated-feeds/{curatedFeedName}/packages",
                new { controller = "CuratedFeeds", action = "ListPackages" });

            routes.MapRoute(
                RouteName.CreateCuratedPackageForm,
                "forms/add-package-to-curated-feed",
                new { controller = "CuratedPackages", action = "CreateCuratedPackageForm" });

            routes.MapRoute(
                RouteName.CuratedPackage,
                "curated-feeds/{curatedFeedName}/curated-packages/{curatedPackageId}",
                new { controller = "CuratedPackages", action = "CuratedPackage" });

            routes.MapRoute(
                RouteName.CuratedPackages,
                "curated-feeds/{curatedFeedName}/curated-packages",
                new { controller = "CuratedPackages", action = "CuratedPackages" });

            // TODO : Most of the routes are essentially of the format api/v{x}/*. We should refactor the code to vary them by the version.
            // V1 Routes
            // If the push url is /api/v1 then NuGet.Core would ping the path to resolve redirection. 
            routes.MapRoute(
                "v1" + RouteName.VerifyPackageKey,
                "api/v1/verifykey/{id}/{version}",
                new { 
                    controller = "Api", 
                    action = "VerifyPackageKey", 
                    id = UrlParameter.Optional, 
                    version = UrlParameter.Optional 
                });

            var downloadRoute = routes.MapRoute(
                "v1" + RouteName.DownloadPackage,
                "api/v1/package/{id}/{version}",
                defaults: new { 
                    controller = "Api", 
                    action = "GetPackageApi", 
                    version = UrlParameter.Optional 
                },
                constraints: new { httpMethod = new HttpMethodConstraint("GET") });

            routes.MapRoute(
                "v1" + RouteName.PushPackageApi,
                "v1/PackageFiles/{apiKey}/nupkg",
                defaults: new { controller = "Api", action = "PushPackageApi" },
                constraints: new { httpMethod = new HttpMethodConstraint("POST") });

            routes.MapRoute(
                "v1" + RouteName.DeletePackageApi,
                "v1/Packages/{apiKey}/{id}/{version}",
                new { controller = "Api", action = "DeletePackages" });

            routes.MapRoute(
                "v1" + RouteName.PublishPackageApi,
                "v1/PublishedPackages/Publish",
                new { controller = "Api", action = "PublishPackage" });

            // Redirected Legacy Routes

            routes.Redirect(
                r => r.MapRoute(
                    "ReportAbuse",
                    "Package/ReportAbuse/{id}/{version}",
                    new { controller = "Packages", action = "ReportAbuse" }),
                permanent: true).To(packageVersionActionRoute);

            routes.Redirect(
                r => r.MapRoute(
                    "PackageActions",
                    "Package/{action}/{id}",
                    new { controller = "Packages", action = "ContactOwners" },
                    // This next bit looks bad, but it's not. It will never change because
                    // it's mapping the legacy routes to the new better routes.
                    new { action = "ContactOwners|ManagePackageOwners" }),
                permanent: true).To(packageActionRoute);

            routes.Redirect(
                r => r.MapRoute(
                    RouteName.ListPackages,
                    "List/Packages",
                    new { controller = "Packages", action = "ListPackages" }),
                permanent: true).To(packageListRoute);

            routes.Redirect(
                r => r.MapRoute(
                    RouteName.DisplayPackage,
                    "List/Packages/{id}/{version}",
                    new { controller = "Packages", action = "DisplayPackage", version = UrlParameter.Optional }),
                permanent: true).To(packageDisplayRoute);

            routes.Redirect(
                r => r.MapRoute(
                    RouteName.NewSubmission,
                    "Contribute/NewSubmission",
                    new { controller = "Packages", action = "UploadPackage" }),
                permanent: true).To(uploadPackageRoute);

            routes.Redirect(
                r => r.MapRoute(
                    "LegacyDownloadRoute",
                    "v1/Package/Download/{id}/{version}",
                    new { controller = "Api", action = "GetPackageApi", version = UrlParameter.Optional }),
                permanent: true).To(downloadRoute);
        }

        public static void RegisterApiV2Routes(RouteCollection routes)
        {
            // V2 routes
            routes.MapRoute(
                RouteName.Team,
                "api/v2/team",
                defaults: new { controller = "Api", action = "Team" },
                constraints: new { httpMethod = new HttpMethodConstraint("GET") });

            routes.MapRoute(
                "v2" + RouteName.VerifyPackageKey,
                "api/v2/verifykey/{id}/{version}",
                new {
                    controller = "Api",
                    action = "VerifyPackageKey",
                    id = UrlParameter.Optional,
                    version = UrlParameter.Optional
                });

            routes.MapRoute(
                "v2CuratedFeeds" + RouteName.DownloadPackage,
                "api/v2/curated-feeds/package/{id}/{version}",
                defaults: new { controller = "Api", action = "GetPackageApi", version = UrlParameter.Optional },
                constraints: new { httpMethod = new HttpMethodConstraint("GET") });

            routes.MapRoute(
                "v2" + RouteName.DownloadPackage,
                "api/v2/package/{id}/{version}",
                defaults: new { controller = "Api", action = "GetPackageApi", version = UrlParameter.Optional },
                constraints: new { httpMethod = new HttpMethodConstraint("GET") });

            routes.MapRoute(
                "v2" + RouteName.PushPackageApi,
                "api/v2/package",
                defaults: new { controller = "Api", action = "PushPackageApi" },
                constraints: new { httpMethod = new HttpMethodConstraint("PUT") });

            routes.MapRoute(
                "v2" + RouteName.DeletePackageApi,
                "api/v2/package/{id}/{version}",
                new { controller = "Api", action = "DeletePackageApi" },
                constraints: new { httpMethod = new HttpMethodConstraint("DELETE") });

            routes.MapRoute(
                "v2" + RouteName.PublishPackageApi,
                "api/v2/package/{id}/{version}",
                new { controller = "Api", action = "PublishPackageApi" },
                constraints: new { httpMethod = new HttpMethodConstraint("POST") });

            routes.MapRoute(
                "v2PackageIds",
                "api/v2/package-ids",
                new { controller = "Api", action = "PackageIDs" });

            routes.MapRoute(
                "v2PackageVersions",
                "api/v2/package-versions/{id}",
                new { controller = "Api", action = "PackageVersions" });

            routes.MapRoute(
                RouteName.StatisticsDownloadsApi,
                "api/v2/stats/downloads/last6weeks",
                defaults: new { controller = "Api", action = "StatisticsDownloadsApi" },
                constraints: new { httpMethod = new HttpMethodConstraint("GET") });

            routes.MapRoute(
                RouteName.ServiceAlert,
                "api/v2/service-alert",
                defaults: new { controller = "Api", action = "ServiceAlert" },
                constraints: new { httpMethod = new HttpMethodConstraint("GET") });

            routes.MapRoute(
                RouteName.Status,
                "api/status",
                new { controller = "Api", action = "StatusApi" });

            routes.MapRoute(
                RouteName.DownloadNuGetExe,
                "nuget.exe",
                new { controller = "Api", action = "GetNuGetExeApi" });
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