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
        private static IGalleryConfigurationService _configuration;
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
            if (_configuration.Current.RequireSSL || url.RequestContext.HttpContext.Request.IsSecureConnection)
                return Uri.UriSchemeHttps;

            return Uri.UriSchemeHttp;
        }

        internal static void SetConfigurationService(IGalleryConfigurationService configurationService)
        {
            _configuration = configurationService;
        }

        internal static string GetSiteRoot(bool useHttps)
        {
            return _configuration.GetSiteRoot(useHttps);
        }

        private static string GetConfiguredSiteHostName()
        {
            // It doesn't matter which value we pass on here for the useHttps parameter.
            // We're just interested in the host, which is the same for both, 
            // as it all results from the same 'NuGetGallery.SiteRoot' URL value.
            var siteRoot = GetSiteRoot(useHttps: true);
            return new Uri(siteRoot).Host;
        }

        public static string Home(this UrlHelper url, bool relativeUrl = true)
        {
            return GetRouteLink(url, RouteName.Home, relativeUrl);
        }

        public static string Statistics(this UrlHelper url, bool relativeUrl = true)
        {
            return GetRouteLink(url, RouteName.StatisticsHome, relativeUrl);
        }

        public static string StatisticsAllPackageDownloads(this UrlHelper url, bool relativeUrl = true)
        {
            return GetRouteLink(url, RouteName.StatisticsPackages, relativeUrl);
        }

        public static string StatisticsAllPackageVersionDownloads(this UrlHelper url, bool relativeUrl = true)
        {
            return GetRouteLink(url, RouteName.StatisticsPackageVersions, relativeUrl);
        }

        public static string StatisticsPackageDownloadByVersion(this UrlHelper url, string id, bool relativeUrl = true)
        {
            var result = GetRouteLink(
                url,
                RouteName.StatisticsPackageDownloadsByVersion,
                relativeUrl,
                routeValues: new RouteValueDictionary
                {
                    { "id", id }
                });

            return result + "?groupby=Version";
        }

        public static string StatisticsPackageDownloadByVersionReport(this UrlHelper url, bool relativeUrl = true)
        {
            return GetRouteLink(url, RouteName.StatisticsPackageDownloadsByVersionReport, relativeUrl);
        }

        public static string StatisticsPackageDownloadsDetail(this UrlHelper url, string id, string version, bool relativeUrl = true)
        {
            var result = GetRouteLink(
                url, 
                RouteName.StatisticsPackageDownloadsDetail, 
                relativeUrl,
                routeValues: new RouteValueDictionary
                {
                    { "id", id },
                    { "version", version }
                });

            return result + "?groupby=ClientName";
        }

        public static string StatisticsPackageDownloadsDetailReport(this UrlHelper url, bool relativeUrl = true)
        {
            return GetRouteLink(url, RouteName.StatisticsPackageDownloadsDetailReport, relativeUrl);
        }

        public static string PackageList(
            this UrlHelper url,
            int page,
            string q,
            bool includePrerelease,
            bool relativeUrl = true)
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

            return GetActionLink(
                url,
                "ListPackages",
                "Packages",
                relativeUrl,
                routeValues);
        }

        public static string CuratedPackage(
            this UrlHelper url,
            string curatedFeedName,
            string id,
            bool relativeUrl = true)
        {
            return GetRouteLink(
                url,
                RouteName.CuratedPackage,
                relativeUrl,
                new RouteValueDictionary
                {
                    { "curatedFeedName", curatedFeedName },
                    { "curatedPackageId", id }
                });
        }

        public static string CuratedPackageList(
            this UrlHelper url,
            int page,
            string q,
            string curatedFeedName,
            bool relativeUrl = true)
        {
            return GetActionLink(
                url,
                "ListPackages",
                "CuratedFeeds",
                relativeUrl,
                routeValues: new RouteValueDictionary
                {
                    { "q", q },
                    { "page", page },
                    { "curatedFeedName", curatedFeedName }
                });
        }

        public static string CuratedFeed(this UrlHelper url, string curatedFeedName, bool relativeUrl = true)
        {
            return GetRouteLink(
                url, 
                RouteName.CuratedFeed, 
                relativeUrl,
                routeValues: new RouteValueDictionary
                {
                    { "name", curatedFeedName }
                });
        }

        public static string PackageList(this UrlHelper url, bool relativeUrl = true)
        {
            return GetRouteLink(url, RouteName.ListPackages, relativeUrl);
        }

        public static string UndoPendingEdits(
            this UrlHelper url,
            IPackageVersionModel package,
            bool relativeUrl = true)
        {
            return GetActionLink(
                url,
                "UndoPendingEdits",
                "Packages",
                relativeUrl,
                routeValues: new RouteValueDictionary
                {
                    { "id", package.Id },
                    { "version", package.Version }
                });
        }

        public static string Package(this UrlHelper url, string id, bool relativeUrl = true)
        {
            return url.Package(id, version: null, relativeUrl: relativeUrl);
        }

        public static string Package(
            this UrlHelper url,
            string id,
            string version,
            bool relativeUrl = true)
        {
            string result = GetRouteLink(
                url,
                RouteName.DisplayPackage,
                relativeUrl,
                routeValues: new RouteValueDictionary
                {
                    { "id", id },
                    { "version", version }
                });

            // Ensure trailing slashes for versionless package URLs, as a fix for package filenames that look like known file extensions
            return version == null ? EnsureTrailingSlash(result) : result;
        }

        public static string Package(this UrlHelper url, Package package, bool relativeUrl = true)
        {
            return url.Package(package.PackageRegistration.Id, package.NormalizedVersion, relativeUrl);
        }

        public static string Package(this UrlHelper url, IPackageVersionModel package, bool relativeUrl = true)
        {
            return url.Package(package.Id, package.Version, relativeUrl);
        }

        public static string Package(this UrlHelper url, PackageRegistration package, bool relativeUrl = true)
        {
            return url.Package(package.Id, relativeUrl);
        }

        public static string PackageDefaultIcon(this UrlHelper url)
        {
            return url.Home(relativeUrl: false).TrimEnd('/')
                + VirtualPathUtility.ToAbsolute("~/Content/Images/packageDefaultIcon-50x50.png", url.RequestContext.HttpContext.Request.ApplicationPath);
        }

        public static string PackageDownload(
            this UrlHelper url,
            int feedVersion,
            string id,
            string version,
            bool relativeUrl = true)
        {
            string result = GetRouteLink(
                url,
                routeName: $"v{feedVersion}{RouteName.DownloadPackage}",
                relativeUrl: false,
                routeValues: new RouteValueDictionary
                {
                    { "Id", id },
                    { "Version", version }
                });

            // Ensure trailing slashes for versionless package URLs, as a fix for package filenames that look like known file extensions
            return version == null ? EnsureTrailingSlash(result) : result;
        }

        public static string ExplorerDeepLink(
            this UrlHelper url,
            int feedVersion,
            string id,
            string version)
        {
            var urlResult = GetRouteLink(
                url,
                routeName: $"v{feedVersion}{RouteName.DownloadPackage}",
                relativeUrl: false,
                routeValues: new RouteValueDictionary
                {
                    { "Id", id }
                });

            urlResult = EnsureTrailingSlash(urlResult);

            return string.Format(CultureInfo.InvariantCulture, PackageExplorerDeepLink, urlResult, id, version);
        }

        public static string LogOn(this UrlHelper url, bool relativeUrl = true)
        {
            return GetRouteLink(
                url,
                RouteName.Authentication,
                relativeUrl,
                routeValues: new RouteValueDictionary
                {
                    { "action", "LogOn" },
                });
        }

        public static string LogOn(this UrlHelper url, string returnUrl, bool relativeUrl = true)
        {
            return GetRouteLink(
                url,
                RouteName.Authentication,
                relativeUrl,
                routeValues: new RouteValueDictionary
                {
                    { "action", "LogOn" },
                    { "returnUrl", returnUrl }
                });
        }

        public static string SignUp(this UrlHelper url, bool relativeUrl = true)
        {
            return GetRouteLink(
                url,
                RouteName.Authentication,
                relativeUrl,
                routeValues: new RouteValueDictionary
                {
                    { "action", "SignUp" }
                });
        }

        public static string SignUp(this UrlHelper url, string returnUrl, bool relativeUrl = true)
        {
            return GetRouteLink(
                url,
                RouteName.Authentication,
                relativeUrl,
                routeValues: new RouteValueDictionary
                {
                    { "action", "SignUp" },
                    { "returnUrl", returnUrl }
                });
        }

        public static string ConfirmationRequired(this UrlHelper url, bool relativeUrl = true)
        {
            return GetActionLink(url, "ConfirmationRequired", "Users", relativeUrl);
        }

        public static string LogOff(this UrlHelper url, bool relativeUrl = true)
        {
            string returnUrl = url.Current();
            // If we're logging off from the Admin Area, don't set a return url
            if (string.Equals(url.RequestContext.RouteData.DataTokens["area"].ToStringOrNull(), "Admin", StringComparison.OrdinalIgnoreCase))
            {
                returnUrl = string.Empty;
            }

            return GetActionLink(
                url,
                "LogOff",
                "Authentication",
                relativeUrl,
                routeValues: new RouteValueDictionary
                {
                    { "returnUrl", returnUrl },
                    { "area", string.Empty }
                });
        }

        public static string Register(this UrlHelper url, bool relativeUrl = true)
        {
            return GetActionLink(url, "LogOn", "Authentication", relativeUrl);
        }

        public static string Search(this UrlHelper url, string searchTerm, bool relativeUrl = true)
        {
            return GetRouteLink(
                url, 
                RouteName.ListPackages, 
                relativeUrl,
                routeValues: new RouteValueDictionary
                {
                    { "q", searchTerm }
                });
        }

        public static string UploadPackage(this UrlHelper url, bool relativeUrl = true)
        {
            return GetRouteLink(url, RouteName.UploadPackage, relativeUrl);
        }

        public static string UploadPackageProgress(this UrlHelper url, bool relativeUrl = true)
        {
            return GetRouteLink(url, RouteName.UploadPackageProgress, relativeUrl);
        }

        public static string User(
            this UrlHelper url,
            User user,
            int page = 1,
            bool relativeUrl = true)
        {
            var routeValues = new RouteValueDictionary
            {
                { "username", user.Username.TrimEnd() }
            };

            if (page != 1)
            {
                routeValues.Add("page", page);
            }

            return GetActionLink(url, "Profiles", "Users", relativeUrl, routeValues);
        }

        public static string User(
            this UrlHelper url,
            string username,
            string scheme = null,
            bool relativeUrl = true)
        {
            return GetActionLink(
                url,
                "Profiles",
                "Users",
                relativeUrl,
                routeValues: new RouteValueDictionary
                {
                    { "username", username }
                });
        }

        public static string EditPackage(
            this UrlHelper url,
            string id,
            string version,
            bool relativeUrl = true)
        {
            if (string.IsNullOrEmpty(version))
            {
                return EnsureTrailingSlash(
                    GetRouteLink(
                        url,
                        RouteName.PackageAction,
                        relativeUrl,
                        routeValues: new RouteValueDictionary
                        {
                            { "action", "Edit" },
                            { "id", id }
                        }));
            }

            return GetRouteLink(
                url,
                RouteName.PackageVersionAction,
                relativeUrl,
                routeValues: new RouteValueDictionary
                {
                    { "action", "Edit" },
                    { "id", id },
                    { "version", version }
                });
        }

        public static string ReflowPackage(
            this UrlHelper url,
            IPackageVersionModel package,
            bool relativeUrl = true)
        {
            return GetActionLink(
                url,
                "Reflow",
                "Packages",
                relativeUrl,
                routeValues: new RouteValueDictionary
                {
                    { "id", package.Id },
                    { "version", package.Version }
                });
        }

        public static string DeletePackage(
            this UrlHelper url,
            IPackageVersionModel package,
            bool relativeUrl = true)
        {
            return GetActionLink(
                url,
                "Delete",
                "Packages",
                relativeUrl,
                routeValues: new RouteValueDictionary
                {
                    { "id", package.Id },
                    { "version", package.Version }
                });
        }

        public static string AccountSettings(
            this UrlHelper url,
            bool relativeUrl = true)
        {
            return GetActionLink(url, "Account", "Users", relativeUrl);
        }

        public static string ReportPackage(
            this UrlHelper url,
            string id,
            string version,
            bool relativeUrl = true)
        {
            return GetActionLink(
                url,
                "ReportMyPackage",
                "Packages",
                relativeUrl,
                routeValues: new RouteValueDictionary
                {
                    { "id", id },
                    { "version", version }
                });
        }

        public static string ReportAbuse(
            this UrlHelper url,
            string id,
            string version,
            bool relativeUrl = true)
        {
            return GetActionLink(
                url,
                "ReportAbuse",
                "Packages",
                relativeUrl,
                routeValues: new RouteValueDictionary
                {
                    { "id", id },
                    { "version", version }
                });
        }

        public static string LinkExternalAccount(this UrlHelper url, string returnUrl, bool relativeUrl = true)
        {
            return GetActionLink(
                url,
                "LinkExternalAccount",
                "Authentication",
                relativeUrl,
                routeValues: new RouteValueDictionary
                {
                    { "ReturnUrl", returnUrl }
                });
        }

        public static string ManageMyApiKeys(this UrlHelper url, bool relativeUrl = true)
        {
            return GetActionLink(url, "ApiKeys", "Users", relativeUrl);
        }

        public static string ManageMyPackages(this UrlHelper url, bool relativeUrl = true)
        {
            return GetActionLink(url, "Packages", "Users", relativeUrl);
        }

        public static string ManagePackageOwners(this UrlHelper url, IPackageVersionModel package, bool relativeUrl = true)
        {
            return GetActionLink(
                url,
                "ManagePackageOwners",
                "Packages",
                relativeUrl,
                routeValues: new RouteValueDictionary
                {
                    { "id", package.Id}
                });
        }

        public static string GetAddPackageOwnerConfirmation(this UrlHelper url, bool relativeUrl = true)
        {
            return GetActionLink(url, "GetAddPackageOwnerConfirmation", "JsonApi", relativeUrl);
        }

        public static string GetPackageOwners(this UrlHelper url, bool relativeUrl = true)
        {
            return GetActionLink(url, "GetPackageOwners", "JsonApi", relativeUrl);
        }

        public static string AddPackageOwner(this UrlHelper url, bool relativeUrl = true)
        {
            return GetActionLink(url, "AddPackageOwner", "JsonApi", relativeUrl);
        }

        public static string RemovePackageOwner(this UrlHelper url, bool relativeUrl = true)
        {
            return GetActionLink(url, "RemovePackageOwner", "JsonApi", relativeUrl);
        }

        public static string ConfirmationUrl(
            this UrlHelper url,
            string action,
            string controller,
            string username,
            string token,
            bool relativeUrl = true)
        {
            return ConfirmationUrl(url, action, controller, username, token, routeValues: null, relativeUrl: relativeUrl);
        }

        public static string ConfirmationUrl(
            this UrlHelper url,
            string action,
            string controller,
            string username,
            string token,
            object routeValues,
            bool relativeUrl = true)
        {
            var rvd = routeValues == null ? new RouteValueDictionary() : new RouteValueDictionary(routeValues);
            rvd["username"] = username;
            rvd["token"] = token;

            return GetActionLink(url, action, controller, relativeUrl, rvd);
        }

        public static string VerifyPackage(this UrlHelper url, bool relativeUrl = true)
        {
            return GetActionLink(url, "VerifyPackage", "Packages", relativeUrl);
        }

        public static string CancelUpload(this UrlHelper url, bool relativeUrl = true)
        {
            return GetActionLink(url, "CancelUpload", "Packages", relativeUrl);
        }

        public static string Downloads(this UrlHelper url, bool relativeUrl = true)
        {
            return GetRouteLink(url, RouteName.Downloads, relativeUrl);
        }

        public static string Contact(this UrlHelper url, bool relativeUrl = true)
        {
            return GetActionLink(url, "Contact", "Pages", relativeUrl);
        }

        public static string ContactOwners(this UrlHelper url, string id, bool relativeUrl = true)
        {
            return GetActionLink(
                url,
                "ContactOwners",
                "Packages",
                relativeUrl,
                routeValues: new RouteValueDictionary
                {
                    { "id", id }
                });
        }

        public static string Terms(this UrlHelper url, bool relativeUrl = true)
        {
            return GetActionLink(url, "Terms", "Pages", relativeUrl);
        }

        public static string Privacy(this UrlHelper url, bool relativeUrl = true)
        {
            return GetActionLink(url, "Privacy", "Pages", relativeUrl);
        }

        public static string About(this UrlHelper url, bool relativeUrl = true)
        {
            return GetActionLink(url, "About", "Pages", relativeUrl);
        }

        public static string Admin(this UrlHelper url, bool relativeUrl = true)
        {
            return GetActionLink(
                url,
                "Index",
                "Home",
                relativeUrl,
                routeValues: new RouteValueDictionary
                {
                    { "area", "Admin" }
                });
        }

        public static string Authenticate(this UrlHelper url, string providerName, string returnUrl, bool relativeUrl = true)
        {
            return GetActionLink(
                url,
                "Authenticate",
                "Authentication",
                relativeUrl,
                routeValues: new RouteValueDictionary
                {
                    { "provider", providerName },
                    { "returnUrl", returnUrl }
                });
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
            if (url != null
                && !url.EndsWith("/", StringComparison.OrdinalIgnoreCase)
                && !url.Contains("?"))
            {
                return url + '/';
            }

            return url;
        }

        private static string GetActionLink(
            UrlHelper url,
            string actionName,
            string controllerName,
            bool relativeUrl,
            RouteValueDictionary routeValues = null
            )
        {
            var protocol = GetProtocol(url);
            var hostName = GetConfiguredSiteHostName();

            var actionLink = url.Action(actionName, controllerName, routeValues, protocol, hostName);

            if (relativeUrl)
            {
                return actionLink.Replace($"{protocol}://{hostName}", string.Empty);
            }

            return actionLink;
        }

        private static string GetRouteLink(
            UrlHelper url,
            string routeName,
            bool relativeUrl,
            RouteValueDictionary routeValues = null)
        {
            var protocol = GetProtocol(url);
            var hostName = GetConfiguredSiteHostName();

            var routeLink = url.RouteUrl(routeName, routeValues, protocol, hostName);

            if (relativeUrl)
            {
                return routeLink.Replace($"{protocol}://{hostName}", string.Empty);
            }

            return routeLink;
        }
    }
}
