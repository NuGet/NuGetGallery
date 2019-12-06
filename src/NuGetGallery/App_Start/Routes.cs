// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.Web.Mvc;
using System.Web.Routing;
using NuGetGallery.Controllers;
using RouteMagic;

namespace NuGetGallery
{
    public static class Routes
    {
        public static void RegisterRoutes(RouteCollection routes, bool feedOnlyMode = false)
        {
            if (!feedOnlyMode)
            {
                RegisterUIRoutes(routes);
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
            RegisterApiV2Routes(routes);
        }

        public static void RegisterUIRoutes(RouteCollection routes)
        {
            routes.MapRoute(
                RouteName.Home,
                "",
                new { controller = "Pages", action = "Home" }); // T4MVC doesn't work with Async Action

            routes.MapRoute(
                RouteName.ErrorReadOnly,
                "errors/readonly",
                new { controller = "Errors", action = "ReadOnlyMode" });

            routes.MapRoute(
                RouteName.Error500,
                "errors/500",
                new { controller = "Errors", action = "InternalError" });

            routes.MapRoute(
                RouteName.Error404,
                "errors/404",
                new { controller = "Errors", action = "NotFound" });

            routes.MapRoute(
                RouteName.Error400,
                "errors/400",
                new { controller = "Errors", action = "BadRequest" });

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
                RouteName.StatisticsPackageDownloadsDetailReport,
                "stats/reports/packages/{id}/{version}",
                new { controller = "Statistics", action = "PackageDownloadsDetailReport" });

            routes.MapRoute(
                RouteName.StatisticsPackageDownloadsByVersion,
                "stats/packages/{id}",
                new { controller = "Statistics", action = "PackageDownloadsByVersion" });

            routes.MapRoute(
                RouteName.StatisticsPackageDownloadsByVersionReport,
                "stats/reports/packages/{id}",
                new { controller = "Statistics", action = "PackageDownloadsByVersionReport" });

            routes.MapRoute(
                RouteName.JsonApi,
                "json/{action}",
                new { controller = "JsonApi" });

            routes.MapRoute(
                RouteName.ManageDeprecationJsonApi,
                "json/deprecation/{action}",
                new { controller = "ManageDeprecationJsonApi" });

            routes.MapRoute(
                RouteName.Contributors,
                "pages/contributors",
                new { controller = "Pages", action = "Contributors" });

            routes.MapRoute(
                RouteName.PagesSimulateError,
                "pages/simulate-error",
                new { controller = "Pages", action = nameof(PagesController.SimulateError) });

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
                "packages/manage/upload",
                new { controller = "Packages", action = "UploadPackage" });

            routes.MapRoute(
                RouteName.UploadPackageProgress,
                "packages/manage/upload-progress",
                new { controller = "Packages", action = "UploadPackageProgress" });

            routes.MapRoute(
                RouteName.VerifyPackage,
                "packages/manage/verify-upload",
                new { controller = "Packages", action = "VerifyPackage" });

            routes.MapRoute(
                RouteName.PreviewReadMe,
                "packages/manage/preview-readme",
                new { controller = "Packages", action = "PreviewReadMe" });

            routes.MapRoute(
                RouteName.CancelUpload,
                "packages/manage/cancel-upload",
                new { controller = "Packages", action = "CancelUpload" });

            routes.MapRoute(
                RouteName.SetRequiredSigner,
                "packages/{id}/required-signer/{username}",
                new { controller = "Packages", action = RouteName.SetRequiredSigner, username = UrlParameter.Optional },
                constraints: new { httpMethod = new HttpMethodConstraint("POST") },
                obfuscationMetadata: new RouteExtensions.ObfuscatedPathMetadata(3, Obfuscator.DefaultTelemetryUserName));

            routes.MapRoute(
                RouteName.PackageOwnerConfirmationRedirect,
                "packages/{id}/owners/{username}/confirm/{token}",
                new { controller = "Packages", action = nameof(PackagesController.ConfirmPendingOwnershipRequestRedirect) },
                constraints: new { httpMethod = new HttpMethodConstraint("GET") },
                obfuscationMetadatas: new[]
                {
                    new RouteExtensions.ObfuscatedPathMetadata(3, Obfuscator.DefaultTelemetryUserName),
                    new RouteExtensions.ObfuscatedPathMetadata(5, Obfuscator.DefaultTelemetryToken)
                });

            routes.MapRoute(
                RouteName.PackageOwnerConfirmation,
                "packages/{id}/owners/{username}/confirm/{token}",
                new { controller = "Packages", action = nameof(PackagesController.ConfirmPendingOwnershipRequest) },
                constraints: new { httpMethod = new HttpMethodConstraint("POST") },
                obfuscationMetadatas: new[]
                {
                    new RouteExtensions.ObfuscatedPathMetadata(3, Obfuscator.DefaultTelemetryUserName),
                    new RouteExtensions.ObfuscatedPathMetadata(5, Obfuscator.DefaultTelemetryToken)
                });

            routes.MapRoute(
                RouteName.PackageOwnerRejectionRedirect,
                "packages/{id}/owners/{username}/reject/{token}",
                new { controller = "Packages", action = nameof(PackagesController.RejectPendingOwnershipRequestRedirect) },
                constraints: new { httpMethod = new HttpMethodConstraint("GET") },
                obfuscationMetadatas: new[]
                {
                    new RouteExtensions.ObfuscatedPathMetadata(3, Obfuscator.DefaultTelemetryUserName),
                    new RouteExtensions.ObfuscatedPathMetadata(5, Obfuscator.DefaultTelemetryToken)
                });

            routes.MapRoute(
                RouteName.PackageOwnerRejection,
                "packages/{id}/owners/{username}/reject/{token}",
                new { controller = "Packages", action = nameof(PackagesController.RejectPendingOwnershipRequest) },
                constraints: new { httpMethod = new HttpMethodConstraint("POST") },
                obfuscationMetadatas: new[]
                {
                    new RouteExtensions.ObfuscatedPathMetadata(3, Obfuscator.DefaultTelemetryUserName),
                    new RouteExtensions.ObfuscatedPathMetadata(5, Obfuscator.DefaultTelemetryToken)
                });

            routes.MapRoute(
                RouteName.PackageOwnerCancellation,
                "packages/{id}/owners/{username}/cancel/{token}",
                new { controller = "Packages", action = "CancelPendingOwnershipRequest" },
                new[]
                {
                    new RouteExtensions.ObfuscatedPathMetadata(3, Obfuscator.DefaultTelemetryUserName),
                    new RouteExtensions.ObfuscatedPathMetadata(5, Obfuscator.DefaultTelemetryToken)
                });

            // We need the following two routes (rather than just one) due to Routing's
            // Consecutive Optional Parameter bug. :(
            var packageDisplayRoute = routes.MapRoute(
                RouteName.DisplayPackage,
                "packages/{id}/{version}",
                new
                {
                    controller = "packages",
                    action = "DisplayPackage",
                    version = UrlParameter.Optional
                },
                new { version = new VersionRouteConstraint() });

            routes.MapRoute(
                RouteName.DisplayPackageFeed,
                "packages/{id}/atom.xml",
                new
                {
                    controller = "Packages",
                    action = nameof(PackagesController.AtomFeed)
                });

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

            routes.MapRoute(
                RouteName.PackageReflowAction,
                "packages/manage/reflow",
                new { controller = "Packages", action = nameof(PackagesController.Reflow) });

            routes.MapRoute(
                RouteName.PackageRevalidateAction,
                "packages/manage/revalidate",
                new { controller = "Packages", action = nameof(PackagesController.Revalidate) });

            routes.MapRoute(
                RouteName.PackageRevalidateSymbolsAction,
                "packages/manage/revalidate-symbols",
                new { controller = "Packages", action = nameof(PackagesController.RevalidateSymbols) });

            var packageVersionActionRoute = routes.MapRoute(
                RouteName.PackageVersionAction,
                "packages/{id}/{version}/{action}",
                new { controller = "Packages" },
                new { version = new VersionRouteConstraint() });

            var packageActionRoute = routes.MapRoute(
                RouteName.PackageAction,
                "packages/{id}/{action}",
                new { controller = "Packages" });

            var packageDeleteRoute = routes.MapRoute(
                RouteName.PackageDeleteAction,
                "packages/manage/delete",
                new { controller = "Packages", action = "Delete" });

            var confirmationRequiredRoute = routes.MapRoute(
                "ConfirmationRequired",
                "account/ConfirmationRequired",
                new { controller = "Users", action = "ConfirmationRequired" });

            routes.MapRoute(
                RouteName.License,
                "packages/{id}/{version}/license",
                new { controller = "Packages", action = "License" });

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
                "RegisterAccount",
                "account/register",
                new { controller = "Authentication", action = "Register" },
                new { httpMethod = new HttpMethodConstraint("POST") });

            routes.MapRoute(
                RouteName.SigninAssistance,
                "account/assistance",
                new { controller = "Authentication", action = "SignInAssistance" },
                new { httpMethod = new HttpMethodConstraint("POST") });

            routes.MapRoute(
                RouteName.LegacyRegister,
                "account/register",
                new { controller = "Authentication", action = "RegisterLegacy" },
                new { httpMethod = new HttpMethodConstraint("GET") });

            routes.MapRoute(
                RouteName.LegacyRegister2,
                "users/account/register",
                new { controller = "Authentication", action = "RegisterLegacy" },
                new { httpMethod = new HttpMethodConstraint("GET") });

            routes.MapRoute(
                RouteName.Authentication,
                "users/account/{action}",
                new { controller = "Authentication" });

            routes.MapRoute(
                RouteName.Profile,
                "profiles/{username}",
                new { controller = "Users", action = "Profiles" },
                new RouteExtensions.ObfuscatedPathMetadata(1, Obfuscator.DefaultTelemetryUserName));

            routes.MapRoute(
                RouteName.GetAccountAvatar,
                "profiles/{accountName}/avatar",
                new { controller = "Users", action = "GetAvatar" },
                new RouteExtensions.ObfuscatedPathMetadata(1, Obfuscator.DefaultTelemetryUserName));

            routes.MapRoute(
                RouteName.GetUserCertificate,
                "account/certificates/{thumbprint}",
                new { controller = "Users", action = "GetCertificate" },
                constraints: new { httpMethod = new HttpMethodConstraint("GET") });

            routes.MapRoute(
                RouteName.DeleteUserCertificate,
                "account/certificates/{thumbprint}",
                new { controller = "Users", action = "DeleteCertificate" },
                constraints: new { httpMethod = new HttpMethodConstraint("DELETE") });

            routes.MapRoute(
                RouteName.GetUserCertificates,
                "account/certificates",
                new { controller = "Users", action = "GetCertificates" },
                constraints: new { httpMethod = new HttpMethodConstraint("GET") });

            routes.MapRoute(
                RouteName.AddUserCertificate,
                "account/certificates",
                new { controller = "Users", action = "AddCertificate" },
                constraints: new { httpMethod = new HttpMethodConstraint("POST") });

            routes.MapRoute(
                RouteName.Send2FAFeedback,
                "account/sendfeedback",
                new { controller = "Users", action = "Send2FAFeedback" },
                new { httpMethod = new HttpMethodConstraint("POST") });

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
                new { controller = "Users", action = "ResetPassword", forgot = true },
                new[]
                {
                    new RouteExtensions.ObfuscatedPathMetadata(2, Obfuscator.DefaultTelemetryUserName),
                    new RouteExtensions.ObfuscatedPathMetadata(3, Obfuscator.DefaultTelemetryToken)
                });

            routes.MapRoute(
                RouteName.PasswordSet,
                "account/setpassword/{username}/{token}",
                new { controller = "Users", action = "ResetPassword", forgot = false },
                new[]
                {
                    new RouteExtensions.ObfuscatedPathMetadata(2, Obfuscator.DefaultTelemetryUserName),
                    new RouteExtensions.ObfuscatedPathMetadata(3, Obfuscator.DefaultTelemetryToken)
                });

            routes.MapRoute(
                RouteName.ConfirmAccount,
                "account/confirm/{accountName}/{token}",
                new { controller = "Users", action = "Confirm" },
                new[]
                {
                    new RouteExtensions.ObfuscatedPathMetadata(2, Obfuscator.DefaultTelemetryUserName),
                    new RouteExtensions.ObfuscatedPathMetadata(3, Obfuscator.DefaultTelemetryToken)
                });

            routes.MapRoute(
                RouteName.ChangeEmailSubscription,
                "account/subscription/change",
                new { controller = "Users", action = "ChangeEmailSubscription" });

            routes.MapRoute(
                RouteName.ChangeMultiFactorAuthentication,
                "account/changeMultiFactorAuthentication",
                new { controller = "Users", action = "ChangeMultiFactorAuthentication" });

            routes.MapRoute(
                RouteName.AdminDeleteAccount,
                "account/delete/{accountName}",
                new { controller = "Users", action = "Delete" },
                new RouteExtensions.ObfuscatedPathMetadata(2, Obfuscator.DefaultTelemetryUserName));

            routes.MapRoute(
                RouteName.UserDeleteAccount,
                "account/delete",
                new { controller = "Users", action = "DeleteRequest" });

            routes.MapRoute(
                RouteName.TransformToOrganization,
                "account/transform",
                new { controller = "Users", action = nameof(UsersController.TransformToOrganization) });

            routes.MapRoute(
                RouteName.TransformToOrganizationConfirmationRedirect,
                "account/transform/confirm/{accountNameToTransform}/{token}",
                new { controller = "Users", action = nameof(UsersController.ConfirmTransformToOrganizationRedirect) },
                new { httpMethod = new HttpMethodConstraint("GET") },
                new[]
                {
                    new RouteExtensions.ObfuscatedPathMetadata(3, Obfuscator.DefaultTelemetryUserName),
                    new RouteExtensions.ObfuscatedPathMetadata(4, Obfuscator.DefaultTelemetryToken)
                });

            routes.MapRoute(
                RouteName.TransformToOrganizationConfirmation,
                "account/transform/confirm/{accountNameToTransform}/{token}",
                new { controller = "Users", action = nameof(UsersController.ConfirmTransformToOrganization) },
                new { httpMethod = new HttpMethodConstraint("POST") },
                new[]
                {
                    new RouteExtensions.ObfuscatedPathMetadata(3, Obfuscator.DefaultTelemetryUserName),
                    new RouteExtensions.ObfuscatedPathMetadata(4, Obfuscator.DefaultTelemetryToken)
                });

            routes.MapRoute(
                RouteName.TransformToOrganizationRejectionRedirect,
                "account/transform/reject/{accountNameToTransform}/{token}",
                new { controller = "Users", action = nameof(UsersController.RejectTransformToOrganizationRedirect) },
                new { httpMethod = new HttpMethodConstraint("GET") },
                new[]
                {
                    new RouteExtensions.ObfuscatedPathMetadata(3, Obfuscator.DefaultTelemetryUserName),
                    new RouteExtensions.ObfuscatedPathMetadata(4, Obfuscator.DefaultTelemetryToken)
                });

            routes.MapRoute(
                RouteName.TransformToOrganizationRejection,
                "account/transform/reject/{accountNameToTransform}/{token}",
                new { controller = "Users", action = nameof(UsersController.RejectTransformToOrganization) },
                new { httpMethod = new HttpMethodConstraint("POST") },
                new[]
                {
                    new RouteExtensions.ObfuscatedPathMetadata(3, Obfuscator.DefaultTelemetryUserName),
                    new RouteExtensions.ObfuscatedPathMetadata(4, Obfuscator.DefaultTelemetryToken)
                });

            routes.MapRoute(
                RouteName.TransformToOrganizationCancellationRedirect,
                "account/transform/cancel/{token}",
                new { controller = "Users", action = nameof(UsersController.CancelTransformToOrganizationRedirect) },
                new { httpMethod = new HttpMethodConstraint("GET") },
                new RouteExtensions.ObfuscatedPathMetadata(3, Obfuscator.DefaultTelemetryToken));

            routes.MapRoute(
                RouteName.TransformToOrganizationCancellation,
                "account/transform/cancel/{token}",
                new { controller = "Users", action = nameof(UsersController.CancelTransformToOrganization) },
                new { httpMethod = new HttpMethodConstraint("POST") },
                new RouteExtensions.ObfuscatedPathMetadata(3, Obfuscator.DefaultTelemetryToken));

            routes.MapRoute(
                RouteName.ApiKeys,
                "account/apikeys",
                new { controller = "Users", action = "ApiKeys" });

            routes.MapRoute(
                RouteName.Account,
                "account/{action}",
                new { controller = "Users", action = "Account" });

            routes.MapRoute(
                RouteName.AddOrganization,
                "organization/add",
                new { controller = "Organizations", action = "Add" });

            routes.MapRoute(
                RouteName.GetOrganizationCertificate,
                "organization/{accountName}/certificates/{thumbprint}",
                new { controller = "Organizations", action = "GetCertificate" },
                constraints: new { httpMethod = new HttpMethodConstraint("GET") },
                obfuscationMetadata: new RouteExtensions.ObfuscatedPathMetadata(1, Obfuscator.DefaultTelemetryUserName));

            routes.MapRoute(
                RouteName.DeleteOrganizationCertificate,
                "organization/{accountName}/certificates/{thumbprint}",
                new { controller = "Organizations", action = "DeleteCertificate" },
                constraints: new { httpMethod = new HttpMethodConstraint("DELETE") },
                obfuscationMetadata: new RouteExtensions.ObfuscatedPathMetadata(1, Obfuscator.DefaultTelemetryUserName));

            routes.MapRoute(
                RouteName.GetOrganizationCertificates,
                "organization/{accountName}/certificates",
                new { controller = "Organizations", action = "GetCertificates" },
                constraints: new { httpMethod = new HttpMethodConstraint("GET") },
                obfuscationMetadata: new RouteExtensions.ObfuscatedPathMetadata(1, Obfuscator.DefaultTelemetryUserName));

            routes.MapRoute(
                RouteName.AddOrganizationCertificate,
                "organization/{accountName}/certificates",
                new { controller = "Organizations", action = "AddCertificate" },
                constraints: new { httpMethod = new HttpMethodConstraint("POST") },
                obfuscationMetadata: new RouteExtensions.ObfuscatedPathMetadata(1, Obfuscator.DefaultTelemetryUserName));

            routes.MapRoute(
                RouteName.OrganizationMemberAddAjax,
                "organization/{accountName}/members/add",
                new { controller = "Organizations", action = nameof(OrganizationsController.AddMember) },
                new RouteExtensions.ObfuscatedPathMetadata(1, Obfuscator.DefaultTelemetryUserName));

            routes.MapRoute(
                RouteName.OrganizationMemberAdd,
                "organization/{accountName}/members/add/{memberName}/{isAdmin}",
                new { controller = "Organizations", action = nameof(OrganizationsController.AddMember) },
                new[]
                {
                    new RouteExtensions.ObfuscatedPathMetadata(1, Obfuscator.DefaultTelemetryUserName),
                    new RouteExtensions.ObfuscatedPathMetadata(4, Obfuscator.DefaultTelemetryUserName)
                });

            routes.MapRoute(
                RouteName.OrganizationMemberConfirmRedirect,
                "organization/{accountName}/members/confirm/{confirmationToken}",
                new { controller = "Organizations", action = nameof(OrganizationsController.ConfirmMemberRequestRedirect) },
                new { httpMethod = new HttpMethodConstraint("GET") },
                new[]
                {
                    new RouteExtensions.ObfuscatedPathMetadata(1, Obfuscator.DefaultTelemetryUserName),
                    new RouteExtensions.ObfuscatedPathMetadata(4, Obfuscator.DefaultTelemetryToken)
                });

            routes.MapRoute(
                RouteName.OrganizationMemberConfirm,
                "organization/{accountName}/members/confirm/{confirmationToken}",
                new { controller = "Organizations", action = nameof(OrganizationsController.ConfirmMemberRequest) },
                new { httpMethod = new HttpMethodConstraint("POST") },
                new[]
                {
                    new RouteExtensions.ObfuscatedPathMetadata(1, Obfuscator.DefaultTelemetryUserName),
                    new RouteExtensions.ObfuscatedPathMetadata(4, Obfuscator.DefaultTelemetryToken)
                });

            routes.MapRoute(
                RouteName.OrganizationMemberRejectRedirect,
                "organization/{accountName}/members/reject/{confirmationToken}",
                new { controller = "Organizations", action = nameof(OrganizationsController.RejectMemberRequestRedirect) },
                new { httpMethod = new HttpMethodConstraint("GET") },
                new[]
                {
                    new RouteExtensions.ObfuscatedPathMetadata(1, Obfuscator.DefaultTelemetryUserName),
                    new RouteExtensions.ObfuscatedPathMetadata(4, Obfuscator.DefaultTelemetryToken)
                });

            routes.MapRoute(
                RouteName.OrganizationMemberReject,
                "organization/{accountName}/members/reject/{confirmationToken}",
                new { controller = "Organizations", action = nameof(OrganizationsController.RejectMemberRequest) },
                new { httpMethod = new HttpMethodConstraint("POST") },
                new[]
                {
                    new RouteExtensions.ObfuscatedPathMetadata(1, Obfuscator.DefaultTelemetryUserName),
                    new RouteExtensions.ObfuscatedPathMetadata(4, Obfuscator.DefaultTelemetryToken)
                });

            routes.MapRoute(
                RouteName.OrganizationMemberCancelAjax,
                "organization/{accountName}/members/cancel",
                new { controller = "Organizations", action = RouteName.OrganizationMemberCancelAjax },
                new RouteExtensions.ObfuscatedPathMetadata(1, Obfuscator.DefaultTelemetryUserName));

            routes.MapRoute(
                RouteName.OrganizationMemberCancel,
                "organization/{accountName}/members/cancel/{memberName}",
                new { controller = "Organizations", action = RouteName.OrganizationMemberCancelAjax },
                new[]
                {
                    new RouteExtensions.ObfuscatedPathMetadata(1, Obfuscator.DefaultTelemetryUserName),
                    new RouteExtensions.ObfuscatedPathMetadata(4, Obfuscator.DefaultTelemetryUserName)
                });

            routes.MapRoute(
                RouteName.OrganizationMemberUpdateAjax,
                "organization/{accountName}/members/update",
                new { controller = "Organizations", action = RouteName.OrganizationMemberUpdateAjax },
                new RouteExtensions.ObfuscatedPathMetadata(1, Obfuscator.DefaultTelemetryUserName));

            routes.MapRoute(
                RouteName.OrganizationMemberUpdate,
                "organization/{accountName}/members/update/{memberName}/{isAdmin}",
                new { controller = "Organizations", action = RouteName.OrganizationMemberUpdateAjax },
                new[]
                {
                    new RouteExtensions.ObfuscatedPathMetadata(1, Obfuscator.DefaultTelemetryUserName),
                    new RouteExtensions.ObfuscatedPathMetadata(4, Obfuscator.DefaultTelemetryUserName)
                });

            routes.MapRoute(
                RouteName.OrganizationMemberDeleteAjax,
                "organization/{accountName}/members/delete",
                new { controller = "Organizations", action = RouteName.OrganizationMemberDeleteAjax },
                new RouteExtensions.ObfuscatedPathMetadata(1, Obfuscator.DefaultTelemetryUserName));

            routes.MapRoute(
                RouteName.OrganizationMemberDelete,
                "organization/{accountName}/members/delete/{memberName}",
                new { controller = "Organizations", action = RouteName.OrganizationMemberDeleteAjax },
                new[]
                {
                    new RouteExtensions.ObfuscatedPathMetadata(1, Obfuscator.DefaultTelemetryUserName),
                    new RouteExtensions.ObfuscatedPathMetadata(4, Obfuscator.DefaultTelemetryUserName)
                });

            routes.MapRoute(
                RouteName.OrganizationAccount,
                "organization/{accountName}/{action}",
                new { controller = "Organizations", action = "ManageOrganization" },
                new RouteExtensions.ObfuscatedPathMetadata(1, Obfuscator.DefaultTelemetryUserName));

            routes.MapRoute(
                RouteName.ChangeOrganizationEmailSubscription,
                "organization/{accountName}/subscription/change",
                new { controller = "Organizations", action = "ChangeEmailSubscription" },
                new RouteExtensions.ObfuscatedPathMetadata(1, Obfuscator.DefaultTelemetryUserName));

            routes.MapRoute(
                RouteName.Downloads,
                "downloads",
                new { controller = "Pages", action = "Downloads" });

            routes.MapRoute(
                RouteName.ExperimentsSearchSideBySide,
                "experiments/search-sxs",
                new { controller = "Experiments", action = nameof(ExperimentsController.SearchSideBySide) });

            // TODO : Most of the routes are essentially of the format api/v{x}/*. We should refactor the code to vary them by the version.
            // V1 Routes
            // If the push url is /api/v1 then NuGet.Core would ping the path to resolve redirection.
            routes.MapRoute(
                "v1" + RouteName.VerifyPackageKey,
                "api/v1/verifykey/{id}/{version}",
                new
                {
                    controller = "Api",
                    action = "VerifyPackageKey",
                    version = UrlParameter.Optional
                });

            routes.MapRoute(
                "v1" + RouteName.CreatePackageVerificationKey,
                "api/v1/package/create-verification-key/{id}/{version}",
                new
                {
                    controller = "Api",
                    action = "CreatePackageVerificationKey",
                    version = UrlParameter.Optional
                });

            var downloadRoute = routes.MapRoute(
                "v1" + RouteName.DownloadPackage,
                "api/v1/package/{id}/{version}",
                defaults: new
                {
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
                new
                {
                    controller = "Api",
                    action = "VerifyPackageKey",
                    version = UrlParameter.Optional
                });

            routes.MapRoute(
                "v2" + RouteName.CreatePackageVerificationKey,
                "api/v2/package/create-verification-key/{id}/{version}",
                new
                {
                    controller = "Api",
                    action = "CreatePackageVerificationKey",
                    version = UrlParameter.Optional
                });

            routes.MapRoute(
                "v2" + RouteName.DeprecatePackageApi,
                "api/v2/package/{id}/deprecations",
                new { controller = "Api", action = RouteName.DeprecatePackageApi },
                constraints: new { httpMethod = new HttpMethodConstraint("PUT") });

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
                "v2" + RouteName.DownloadSymbolsPackage,
                "api/v2/symbolpackage/{id}/{version}",
                defaults: new { controller = "Api", action = "GetSymbolPackageApi", version = UrlParameter.Optional },
                constraints: new { httpMethod = new HttpMethodConstraint("GET") });

            routes.MapRoute(
                "v2" + RouteName.PushPackageApi,
                "api/v2/package",
                defaults: new { controller = "Api", action = "PushPackageApi" },
                constraints: new { httpMethod = new HttpMethodConstraint("PUT") });

            routes.MapRoute(
                "v2" + RouteName.PushSymbolPackageApi,
                "api/v2/symbolpackage",
                defaults: new { controller = "Api", action = "PushSymbolPackageApi" },
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
                "v2Query",
                "api/v2/query",
                new { controller = "Api", action = "Query" });

            routes.MapRoute(
                RouteName.StatisticsDownloadsApi,
                "api/v2/stats/downloads/last6weeks",
                defaults: new { controller = "Api", action = "StatisticsDownloadsApi" },
                constraints: new { httpMethod = new HttpMethodConstraint("GET") });

            routes.MapRoute(
                RouteName.Status,
                "api/status",
                new { controller = "Api", action = "StatusApi" });

            routes.MapRoute(
                RouteName.HealthProbe,
                "api/health-probe",
                new { controller = "Api", action = "HealthProbeApi" });

            routes.MapRoute(
                RouteName.DownloadNuGetExe,
                "nuget.exe",
                new { controller = "Api", action = "GetNuGetExeApi" });

            routes.MapRoute(
                RouteName.ApiSimulateError,
                "api/simulate-error",
                new { controller = "Api", action = nameof(ApiController.SimulateError) });
        }
    }
}