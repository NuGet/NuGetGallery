// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGetGallery.Configuration;
using System;
using System.Globalization;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace NuGetGallery
{
    public static class UrlExtensions
    {
        private static ConfigurationService _configuration;
        private const string PackageExplorerDeepLink = @"https://npe.codeplex.com/releases/clickonce/NuGetPackageExplorer.application?url={0}&id={1}&version={2}";

        // Shorthand for current url
        public static string Current(this UrlHelper url)
        {
            return url.RequestContext.HttpContext.Request.RawUrl;
        }

        public static string Absolute(this UrlHelper url, string path)
        {
            UriBuilder builder = GetCanonicalUrl(url);
            if (path.StartsWith("~/", StringComparison.OrdinalIgnoreCase))
            {
                path = VirtualPathUtility.ToAbsolute(path, url.RequestContext.HttpContext.Request.ApplicationPath);
            }
            builder.Path = path;
            return builder.Uri.AbsoluteUri;
        }

        private static string GetProtocol(UrlHelper url)
        {
            return url.RequestContext.HttpContext.Request.IsSecureConnection ? Uri.UriSchemeHttps : Uri.UriSchemeHttp;
        }
                
        internal static void SetConfigurationService(ConfigurationService configurationService)
        {
            _configuration = configurationService;
        }

        private static string GetConfiguredSiteHostName(UrlHelper url)
        {
            var siteRoot = _configuration.GetSiteRoot(useHttps: url.RequestContext.HttpContext.Request.IsSecureConnection);
            return new Uri(siteRoot).Host;
        }

        public static string Home(this UrlHelper url)
        {
            return url.RouteUrl(
                RouteName.Home,
                routeValues: null,
                protocol: GetProtocol(url),
                hostName: GetConfiguredSiteHostName(url));
        }

        public static string Statistics(this UrlHelper url)
        {
            return url.RouteUrl(
                RouteName.StatisticsHome,
                routeValues: null,
                protocol: GetProtocol(url),
                hostName: GetConfiguredSiteHostName(url));
        }

        public static string StatisticsAllPackageDownloads(this UrlHelper url)
        {
            return url.RouteUrl(
                RouteName.StatisticsPackages,
                routeValues: null,
                protocol: GetProtocol(url),
                hostName: GetConfiguredSiteHostName(url));
        }

        public static string StatisticsAllPackageVersionDownloads(this UrlHelper url)
        {
            return url.RouteUrl(
                RouteName.StatisticsPackageVersions,
                routeValues: null,
                protocol: GetProtocol(url),
                hostName: GetConfiguredSiteHostName(url));
        }

        public static string StatisticsPackageDownloadByVersion(this UrlHelper url, string id)
        {
            string result = url.RouteUrl(
                RouteName.StatisticsPackageDownloadsByVersion,
                routeValues: new RouteValueDictionary
                {
                    { "id", id }
                },
                protocol: GetProtocol(url),
                hostName: GetConfiguredSiteHostName(url));

            return result + "?groupby=Version";
        }

        public static string StatisticsPackageDownloadByVersionReport(this UrlHelper url)
        {
            return url.RouteUrl(
                RouteName.StatisticsPackageDownloadsByVersionReport,
                routeValues: null,
                protocol: GetProtocol(url),
                hostName: GetConfiguredSiteHostName(url));
        }

        public static string StatisticsPackageDownloadsDetail(this UrlHelper url, string id, string version)
        {
            string result = url.RouteUrl(
                RouteName.StatisticsPackageDownloadsDetail,
                routeValues: new RouteValueDictionary
                {
                    { "id", id },
                    { "version", version }
                },
                protocol: GetProtocol(url),
                hostName: GetConfiguredSiteHostName(url));

            return result + "?groupby=ClientName";
        }

        public static string StatisticsPackageDownloadsDetailReport(this UrlHelper url)
        {
            return url.RouteUrl(
                RouteName.StatisticsPackageDownloadsDetailReport,
                routeValues: null,
                protocol: GetProtocol(url),
                hostName: GetConfiguredSiteHostName(url));
        }

        public static string PackageList(this UrlHelper url, int page, string q, bool includePrerelease)
        {
            var routeValues = new RouteValueDictionary();

            if (page > 1)
            {
                routeValues["page"] = page;
            }

            if (!string.IsNullOrWhiteSpace(q))
            {
                routeValues["q"] = q;
            }

            if (!includePrerelease)
            {
                routeValues["prerel"] = "false";
            }

            return url.Action(
                actionName: "ListPackages",
                controllerName: "Packages",
                routeValues: routeValues,
                protocol: GetProtocol(url),
                hostName: GetConfiguredSiteHostName(url));
        }

        public static string CuratedPackage(this UrlHelper url, string curatedFeedName, string id, string scheme = null)
        {
            return url.RouteUrl(
                RouteName.CuratedPackage,
                new RouteValueDictionary
                {
                    { "curatedFeedName", curatedFeedName },
                    { "curatedPackageId", id }
                },
                protocol: scheme ?? GetProtocol(url),
                hostName: GetConfiguredSiteHostName(url));
        }

        public static string CuratedPackageList(this UrlHelper url, int page, string q, string curatedFeedName)
        {
            return url.Action(
                actionName: "ListPackages",
                controllerName: "CuratedFeeds",
                routeValues: new RouteValueDictionary
                {
                    { "q", q },
                    { "page", page },
                    { "curatedFeedName", curatedFeedName }
                },
                protocol: GetProtocol(url),
                hostName: GetConfiguredSiteHostName(url));

        }

        public static string CuratedFeed(this UrlHelper url, string curatedFeedName)
        {
            return url.RouteUrl(
                RouteName.CuratedFeed,
                routeValues: new RouteValueDictionary
                {
                    { "name", curatedFeedName }
                },
                protocol: GetProtocol(url),
                hostName: GetConfiguredSiteHostName(url));
        }

        public static string PackageList(this UrlHelper url)
        {
            return url.RouteUrl(
                RouteName.ListPackages,
                routeValues: null,
                protocol: GetProtocol(url),
                hostName: GetConfiguredSiteHostName(url));
        }

        public static string UndoPendingEdits(this UrlHelper url, IPackageVersionModel package)
        {
            return url.Action(
                actionName: "UndoPendingEdits",
                controllerName: "Packages",
                routeValues: new RouteValueDictionary
                {
                    { "id", package.Id },
                    { "version", package.Version }
                },
                protocol: GetProtocol(url),
                hostName: GetConfiguredSiteHostName(url));
        }

        public static string Package(this UrlHelper url, string id)
        {
            return url.Package(id, null, scheme: null);
        }

        public static string Package(this UrlHelper url, string id, string version, string scheme = null)
        {
            string result = url.RouteUrl(
                RouteName.DisplayPackage,
                new RouteValueDictionary
                {
                    { "id", id },
                    { "version", version }
                },
                protocol: scheme ?? GetProtocol(url),
                hostName: GetConfiguredSiteHostName(url));

            // Ensure trailing slashes for versionless package URLs, as a fix for package filenames that look like known file extensions
            return version == null ? EnsureTrailingSlash(result) : result;
        }

        public static string Package(this UrlHelper url, Package package)
        {
            return url.Package(package.PackageRegistration.Id, package.NormalizedVersion);
        }

        public static string Package(this UrlHelper url, IPackageVersionModel package)
        {
            return url.Package(package.Id, package.Version);
        }

        public static string Package(this UrlHelper url, PackageRegistration package)
        {
            return url.Package(package.Id);
        }

        public static string PackageDefaultIcon(this UrlHelper url)
        {
            return url.Home().TrimEnd('/') + VirtualPathUtility.ToAbsolute("~/Content/Images/packageDefaultIcon-50x50.png", url.RequestContext.HttpContext.Request.ApplicationPath);
        }

        public static string PackageDownload(this UrlHelper url, int feedVersion, string id, string version)
        {
            string result = url.RouteUrl(
                routeName: $"v{feedVersion}{RouteName.DownloadPackage}",
                routeValues: new RouteValueDictionary
                {
                    { "Id", id },
                    { "Version", version }
                },
                protocol: GetProtocol(url),
                hostName: GetConfiguredSiteHostName(url));

            // Ensure trailing slashes for versionless package URLs, as a fix for package filenames that look like known file extensions
            return version == null ? EnsureTrailingSlash(result) : result;
        }

        public static string ExplorerDeepLink(this UrlHelper url, int feedVersion, string id, string version)
        {
            string urlResult = url.RouteUrl(
                routeName: $"v{feedVersion}{RouteName.DownloadPackage}",
                routeValues: new RouteValueDictionary
                {
                    { "Id", id }
                },
                protocol: GetProtocol(url),
                hostName: GetConfiguredSiteHostName(url));

            urlResult = EnsureTrailingSlash(urlResult);

            return String.Format(CultureInfo.InvariantCulture, PackageExplorerDeepLink, urlResult, id, version);
        }

        public static string LogOn(this UrlHelper url)
        {
            return url.RouteUrl(
                RouteName.Authentication,
                routeValues: new RouteValueDictionary
                {
                    { "action", "LogOn" }
                },
                protocol: GetProtocol(url),
                hostName: GetConfiguredSiteHostName(url));
        }

        public static string LogOn(this UrlHelper url, string returnUrl)
        {
            return url.RouteUrl(
                RouteName.Authentication,
                routeValues: new RouteValueDictionary
                {
                    { "action", "LogOn" },
                    { "returnUrl", returnUrl }
                },
                protocol: GetProtocol(url),
                hostName: GetConfiguredSiteHostName(url));
        }

        public static string SignUp(this UrlHelper url)
        {
            return url.RouteUrl(
                RouteName.Authentication,
                routeValues: new RouteValueDictionary
                {
                    { "action", "SignUp" }
                },
                protocol: GetProtocol(url),
                hostName: GetConfiguredSiteHostName(url));
        }

        public static string SignUp(this UrlHelper url, string returnUrl)
        {
            return url.RouteUrl(
                RouteName.Authentication,
                routeValues: new RouteValueDictionary
                {
                    { "action", "SignUp" },
                    { "returnUrl", returnUrl }
                },
                protocol: GetProtocol(url),
                hostName: GetConfiguredSiteHostName(url));
        }

        public static string ConfirmationRequired(this UrlHelper url)
        {
            return url.Action(
                "ConfirmationRequired",
                controllerName: "Users",
                routeValues: null,
                protocol: GetProtocol(url),
                hostName: GetConfiguredSiteHostName(url));
        }

        public static string LogOff(this UrlHelper url)
        {
            string returnUrl = url.Current();
            // If we're logging off from the Admin Area, don't set a return url
            if (String.Equals(url.RequestContext.RouteData.DataTokens["area"].ToStringOrNull(), "Admin", StringComparison.OrdinalIgnoreCase))
            {
                returnUrl = String.Empty;
            }
            return url.Action(
                "LogOff",
                "Authentication",
                routeValues: new RouteValueDictionary
                {
                    { "returnUrl", returnUrl },
                    { "area", string.Empty }
                },
                protocol: GetProtocol(url),
                hostName: GetConfiguredSiteHostName(url));
        }

        public static string Register(this UrlHelper url)
        {
            return url.Action(
                actionName: "LogOn",
                controllerName: "Authentication",
                routeValues: null,
                protocol: GetProtocol(url),
                hostName: GetConfiguredSiteHostName(url));
        }

        public static string Search(this UrlHelper url, string searchTerm)
        {
            return url.RouteUrl(
                RouteName.ListPackages,
                routeValues: new RouteValueDictionary
                {
                    { "q", searchTerm }
                },
                protocol: GetProtocol(url),
                hostName: GetConfiguredSiteHostName(url));
        }

        public static string UploadPackage(this UrlHelper url)
        {
            return url.RouteUrl(
                RouteName.UploadPackage,
                routeValues: null,
                protocol: GetProtocol(url),
                hostName: GetConfiguredSiteHostName(url));
        }

        public static string UploadPackageProgress(this UrlHelper url)
        {
            return url.RouteUrl(
                RouteName.UploadPackageProgress,
                routeValues: null,
                protocol: GetProtocol(url),
                hostName: GetConfiguredSiteHostName(url));
        }

        public static string User(this UrlHelper url, User user, int page = 1, string scheme = null)
        {
            var configuredSiteHostName = GetConfiguredSiteHostName(url);
            if (page == 1)
            {
                return url.Action(
                    actionName: "Profiles",
                    controllerName: "Users",
                    routeValues: new RouteValueDictionary
                    {
                        { "username", user.Username.TrimEnd() }
                    },
                    protocol: scheme ?? GetProtocol(url),
                    hostName: configuredSiteHostName);
            }
            else
            {
                return url.Action(
                    actionName: "Profiles",
                    controllerName: "Users",
                    routeValues: new RouteValueDictionary
                    {
                        { "username", user.Username.TrimEnd() },
                        { "page", page }
                    },
                    protocol: scheme ?? GetProtocol(url),
                    hostName: configuredSiteHostName);
            }
        }

        public static string User(this UrlHelper url, string username, string scheme = null)
        {
            return url.Action(
                actionName: "Profiles",
                controllerName: "Users",
                routeValues: new RouteValueDictionary
                {
                    { "username", username }
                },
                protocol: scheme ?? GetProtocol(url),
                hostName: GetConfiguredSiteHostName(url));
        }

        public static string EditPackage(this UrlHelper url, string id, string version)
        {
            if (String.IsNullOrEmpty(version))
            {
                return EnsureTrailingSlash(
                    url.RouteUrl(
                        RouteName.PackageAction,
                        routeValues: new RouteValueDictionary
                        {
                            { "action", "Edit" },
                            { "id", id }
                        },
                        protocol: GetProtocol(url),
                        hostName: GetConfiguredSiteHostName(url)));
            }

            return url.RouteUrl(
                RouteName.PackageVersionAction,
                routeValues: new RouteValueDictionary
                {
                    { "action", "Edit" },
                    { "id", id },
                    { "version", version }
                },
                protocol: GetProtocol(url),
                hostName: GetConfiguredSiteHostName(url));
        }

        public static string ReflowPackage(this UrlHelper url, IPackageVersionModel package)
        {
            return url.Action(
                actionName: "Reflow",
                controllerName: "Packages",
                routeValues: new RouteValueDictionary
                {
                    { "id", package.Id },
                    { "version", package.Version }
                },
                protocol: GetProtocol(url),
                hostName: GetConfiguredSiteHostName(url));
        }

        public static string DeletePackage(this UrlHelper url, IPackageVersionModel package)
        {
            return url.Action(
                actionName: "Delete",
                controllerName: "Packages",
                routeValues: new RouteValueDictionary
                {
                    { "id", package.Id },
                    { "version", package.Version }
                },
                protocol: GetProtocol(url),
                hostName: GetConfiguredSiteHostName(url));
        }

        public static string AccountSettings(this UrlHelper url, string scheme = null)
        {
            return url.Action(
                actionName: "Account",
                controllerName: "Users",
                routeValues: null,
                protocol: scheme ?? GetProtocol(url),
                hostName: GetConfiguredSiteHostName(url));
        }

        public static string ReportPackage(this UrlHelper url, string id, string version, string scheme = null)
        {
            return url.Action(
                actionName: "ReportMyPackage",
                controllerName: "Packages",
                routeValues: new RouteValueDictionary
                {
                    { "id", id },
                    { "version", version }
                },
                protocol: scheme ?? GetProtocol(url),
                hostName: GetConfiguredSiteHostName(url));
        }

        public static string ReportAbuse(this UrlHelper url, string id, string version, string scheme = null)
        {
            return url.Action(
                actionName: "ReportAbuse",
                controllerName: "Packages",
                routeValues: new RouteValueDictionary
                {
                    { "id", id },
                    { "version", version }
                },
                protocol: scheme ?? GetProtocol(url),
                hostName: GetConfiguredSiteHostName(url));
        }

        public static string LinkExternalAccount(this UrlHelper url, string returnUrl)
        {
            return url.Action(
                actionName: "LinkExternalAccount",
                controllerName: "Authentication",
                routeValues: new RouteValueDictionary
                {
                    { "ReturnUrl", returnUrl }
                },
                protocol: GetProtocol(url),
                hostName: GetConfiguredSiteHostName(url));
        }

        public static string ManageMyApiKeys(this UrlHelper url)
        {
            return url.Action(
                actionName: "ApiKeys",
                controllerName: "Users",
                routeValues: null,
                protocol: GetProtocol(url),
                hostName: GetConfiguredSiteHostName(url));
        }

        public static string ManageMyPackages(this UrlHelper url)
        {
            return url.Action(
                actionName: "Packages",
                controllerName: "Users",
                routeValues: null,
                protocol: GetProtocol(url),
                hostName: GetConfiguredSiteHostName(url));
        }

        public static string ManagePackageOwners(this UrlHelper url, IPackageVersionModel package)
        {
            return url.Action(
                actionName: "ManagePackageOwners",
                controllerName: "Packages",
                routeValues: new RouteValueDictionary
                {
                    { "id", package.Id}
                },
                protocol: GetProtocol(url),
                hostName: GetConfiguredSiteHostName(url));
        }

        public static string GetAddPackageOwnerConfirmation(this UrlHelper url)
        {
            return url.Action(
                "GetAddPackageOwnerConfirmation", 
                "JsonApi",
                routeValues: null,
                protocol: GetProtocol(url),
                hostName: GetConfiguredSiteHostName(url));
        }

        public static string GetPackageOwners(this UrlHelper url)
        {
            return url.Action(
                "GetPackageOwners",                 
                "JsonApi",
                routeValues: null,
                protocol: GetProtocol(url),
                hostName: GetConfiguredSiteHostName(url));
        }

        public static string AddPackageOwner(this UrlHelper url)
        {
            return url.Action(
                "AddPackageOwner", 
                "JsonApi",
                routeValues: null,
                protocol: GetProtocol(url),
                hostName: GetConfiguredSiteHostName(url));
        }

        public static string RemovePackageOwner(this UrlHelper url)
        {
            return url.Action(
                "RemovePackageOwner", 
                "JsonApi",
                routeValues: null,
                protocol: GetProtocol(url),
                hostName: GetConfiguredSiteHostName(url));
        }

        public static string ConfirmationUrl(this UrlHelper url, string action, string controller, string username, string token)
        {
            return ConfirmationUrl(url, action, controller, username, token, null);
        }

        public static string ConfirmationUrl(this UrlHelper url, string action, string controller, string username, string token, object routeValues)
        {
            var rvd = routeValues == null ? new RouteValueDictionary() : new RouteValueDictionary(routeValues);
            rvd["username"] = username;
            rvd["token"] = token;
            return url.Action(
                action,
                controller,
                rvd,
                url.RequestContext.HttpContext.Request.Url.Scheme,
                GetConfiguredSiteHostName(url));
        }

        public static string VerifyPackage(this UrlHelper url)
        {
            return url.Action(
                actionName: "VerifyPackage",
                controllerName: "Packages",
                routeValues: null,
                protocol: GetProtocol(url),
                hostName: GetConfiguredSiteHostName(url));
        }

        public static string CancelUpload(this UrlHelper url)
        {
            return url.Action(
                actionName: "CancelUpload",
                controllerName: "Packages",
                routeValues: null,
                protocol: GetProtocol(url),
                hostName: GetConfiguredSiteHostName(url));
        }

        public static string Downloads(this UrlHelper url)
        {
            return url.RouteUrl(
                RouteName.Downloads,
                routeValues: null,
                protocol: GetProtocol(url),
                hostName: GetConfiguredSiteHostName(url));
        }

        public static string Contact(this UrlHelper url)
        {
            return url.Action(
                actionName: "Contact",
                controllerName: "Pages",
                routeValues: null,
                protocol: GetProtocol(url),
                hostName: GetConfiguredSiteHostName(url));
        }

        public static string ContactOwners(this UrlHelper url, string id)
        {
            return url.Action(
                actionName: "ContactOwners",
                controllerName: "Packages",
                routeValues: new RouteValueDictionary
                {
                    { "id", id }
                },
                protocol: GetProtocol(url),
                hostName: GetConfiguredSiteHostName(url));
        }

        public static string Terms(this UrlHelper url)
        {
            return url.Action(
                actionName: "Terms",
                controllerName: "Pages",
                routeValues: null,
                protocol: GetProtocol(url),
                hostName: GetConfiguredSiteHostName(url));
        }

        public static string Privacy(this UrlHelper url)
        {
            return url.Action(
                actionName: "Privacy",
                controllerName: "Pages",
                routeValues: null,
                protocol: GetProtocol(url),
                hostName: GetConfiguredSiteHostName(url));
        }

        public static string About(this UrlHelper url)
        {
            return url.Action(
                actionName: "About",
                controllerName: "Pages",
                routeValues: null,
                protocol: GetProtocol(url),
                hostName: GetConfiguredSiteHostName(url));
        }

        public static string Admin(this UrlHelper url)
        {
            return url.Action(
                "Index",
                "Home",
                routeValues: new RouteValueDictionary
                {
                    { "area", "Admin" }
                },
                protocol: GetProtocol(url),
                hostName: GetConfiguredSiteHostName(url));
        }

        public static string Authenticate(this UrlHelper url, string providerName, string returnUrl)
        {
            return url.Action(
                actionName: "Authenticate",
                controllerName: "Authentication",
                routeValues: new RouteValueDictionary
                {
                    { "provider", providerName },
                    { "returnUrl", returnUrl }
                },
                protocol: GetProtocol(url),
                hostName: GetConfiguredSiteHostName(url));
        }
        
        private static UriBuilder GetCanonicalUrl(UrlHelper url)
        {
            UriBuilder builder = new UriBuilder(url.RequestContext.HttpContext.Request.Url);
            builder.Query = String.Empty;
            if (builder.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
            {
                builder.Host = builder.Host.Substring(4);
            }
            return builder;
        }

        internal static string EnsureTrailingSlash(string url)
        {
            if (url != null && !url.EndsWith("/", StringComparison.OrdinalIgnoreCase))
            {
                return url + '/';
            }

            return url;
        }
    }
}
