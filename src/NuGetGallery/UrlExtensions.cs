using System;
using System.Globalization;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace NuGetGallery
{
    public static class UrlExtensions
    {
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

        public static string Home(this UrlHelper url)
        {
            return url.RouteUrl(RouteName.Home);
        }

        public static string Statistics(this UrlHelper url)
        {
            return url.RouteUrl(RouteName.StatisticsHome);
        }

        public static string StatisticsAllPackageDownloads(this UrlHelper url)
        {
            return url.RouteUrl(RouteName.StatisticsPackages);
        }

        public static string StatisticsAllPackageVersionDownloads(this UrlHelper url)
        {
            return url.RouteUrl(RouteName.StatisticsPackageVersions);
        }

        public static string StatisticsPackageDownloadByVersion(this UrlHelper url, string id)
        {
            string result = url.RouteUrl(RouteName.StatisticsPackageDownloadsByVersion, new { id });

            return result + "?groupby=Version";
        }

        public static string StatisticsPackageDownloadsDetail(this UrlHelper url, string id, string version)
        {
            string result = url.RouteUrl(RouteName.StatisticsPackageDownloadsDetail, new { id, version });

            return result + "?groupby=ClientName";
        }

        public static string PackageList(this UrlHelper url, int page, string q)
        {
            return url.Action("ListPackages", "Packages", new
            {
                q,
                page
            });
        }



        public static string CuratedPackageList(this UrlHelper url, int page, string q, string curatedFeedName)
        {
            return url.Action("ListPackages", "CuratedFeeds", new
            {
                q,
                page,
                curatedFeedName
            });

        }

        public static string PackageList(this UrlHelper url)
        {
            return url.RouteUrl(RouteName.ListPackages);
        }

        public static string UndoPendingEdits(this UrlHelper url, IPackageVersionModel package)
        {
            return url.Action(actionName: "UndoPendingEdits", controllerName: "Packages", routeValues: new { id = package.Id, version = package.Version });
        }

        public static string Package(this UrlHelper url, string id)
        {
            return url.Package(id, null, scheme: null);
        }

        public static string Package(this UrlHelper url, string id, string version, string scheme = null)
        {
            string result = url.RouteUrl(RouteName.DisplayPackage, new { id, version }, protocol: scheme);

            // Ensure trailing slashes for versionless package URLs, as a fix for package filenames that look like known file extensions
            return version == null ? EnsureTrailingSlash(result) : result;
        }

        public static string Package(this UrlHelper url, Package package)
        {
            return url.Package(package.PackageRegistration.Id, package.Version);
        }

        public static string Package(this UrlHelper url, IPackageVersionModel package)
        {
            return url.Package(package.Id, package.Version);
        }

        public static string Package(this UrlHelper url, PackageRegistration package)
        {
            return url.Package(package.Id);
        }

        public static string PackageGallery(this UrlHelper url, string id, string version)
        {
            string protocol = url.RequestContext.HttpContext.Request.IsSecureConnection ? "https" : "http";
            string result = url.RouteUrl(RouteName.DisplayPackage, new { Id = id, Version = version }, protocol: protocol);

            // Ensure trailing slashes for versionless package URLs, as a fix for package filenames that look like known file extensions
            return version == null ? EnsureTrailingSlash(result) : result;
        }

        public static string PackageDefaultIcon(this UrlHelper url)
        {
            string protocol = url.RequestContext.HttpContext.Request.IsSecureConnection ? "https" : "http";
            string result = url.RouteUrl(RouteName.Home, null, protocol: protocol);
            result = result.TrimEnd('/') + VirtualPathUtility.ToAbsolute("~/Content/Images/packageDefaultIcon-50x50.png", url.RequestContext.HttpContext.Request.ApplicationPath);
            return result;
        }

        public static string PackageDownload(this UrlHelper url, int feedVersion, string id, string version)
        {
            string routeName = "v" + feedVersion + RouteName.DownloadPackage;
            string protocol = url.RequestContext.HttpContext.Request.IsSecureConnection ? "https" : "http";
            string result = url.RouteUrl(routeName, new { Id = id, Version = version }, protocol: protocol);

            // Ensure trailing slashes for versionless package URLs, as a fix for package filenames that look like known file extensions
            return version == null ? EnsureTrailingSlash(result) : result;
        }

        public static string ExplorerDeepLink(this UrlHelper url, int feedVersion, string id, string version)
        {
            string routeName = "v" + feedVersion + RouteName.DownloadPackage;
            string protocol = url.RequestContext.HttpContext.Request.IsSecureConnection ? "https" : "http";
            string urlResult = url.RouteUrl(routeName, new { Id = id }, protocol: protocol);

            urlResult = EnsureTrailingSlash(urlResult);

            return String.Format(CultureInfo.InvariantCulture, PackageExplorerDeepLink, urlResult, id, version);
        }

        public static string LogOn(this UrlHelper url)
        {
            return url.RouteUrl(RouteName.Authentication, new { action = "LogOn" });
        }

        public static string LogOn(this UrlHelper url, string returnUrl)
        {
            return url.RouteUrl(RouteName.Authentication, new { action = "LogOn", returnUrl = returnUrl });
        }

        public static string ConfirmationRequired(this UrlHelper url)
        {
            return url.Action("ConfirmationRequired", controllerName: "Users");
        }

        public static string LogOff(this UrlHelper url)
        {
            string returnUrl = url.Current();
            // If we're logging off from the Admin Area, don't set a return url
            if (String.Equals(url.RequestContext.RouteData.DataTokens["area"].ToStringOrNull(), "Admin", StringComparison.OrdinalIgnoreCase))
            {
                returnUrl = String.Empty;
            }
            return url.Action("LogOff", "Authentication", new { returnUrl, area = "" });
        }

        public static string Register(this UrlHelper url)
        {
            return url.Action(actionName: "LogOn", controllerName: "Authentication");
        }

        public static string Search(this UrlHelper url, string searchTerm)
        {
            return url.RouteUrl(RouteName.ListPackages, new { q = searchTerm });
        }

        public static string UploadPackage(this UrlHelper url)
        {
            return url.RouteUrl(RouteName.UploadPackage);
        }

        public static string User(this UrlHelper url, User user, int page = 1, string scheme = null)
        {
            string result;
            if (page == 1)
            {
                result = url.Action(actionName: "Profiles",
                                    controllerName: "Users",
                                    routeValues: new { username = user.Username },
                                    protocol: scheme);
            }
            else
            {
                result = url.Action(actionName: "Profiles",
                                    controllerName: "Users",
                                    routeValues: new { username = user.Username, page = page },
                                    protocol: scheme);
            }


            return result;
        }

        public static string UserShowAllPackages(this UrlHelper url, string username, string scheme = null)
        {
            string result;
                result = url.Action(actionName: "Profiles",
                                    controllerName: "Users",
                                    routeValues: new { username = username, showAllPackages = true },
                                    protocol: scheme);
            return result;
        }

        public static string EditPackage(this UrlHelper url, string id, string version)
        {
            if (String.IsNullOrEmpty(version))
            {
                return EnsureTrailingSlash(url.RouteUrl(RouteName.PackageAction, new { action = "Edit", id }));
            }

            return url.RouteUrl(RouteName.PackageVersionAction, new { action = "Edit", id, version });
        }

        public static string DeletePackage(this UrlHelper url, IPackageVersionModel package)
        {
            return url.Action(
                actionName: "Delete",
                controllerName: "Packages",
                routeValues: new
                {
                    id = package.Id,
                    version = package.Version
                });
        }

        public static string ManagePackageOwners(this UrlHelper url, IPackageVersionModel package)
        {
            return url.Action(
                actionName: "ManagePackageOwners",
                controllerName: "Packages",
                routeValues: new
                {
                    id = package.Id,
                    version = package.Version
                });
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
                url.RequestContext.HttpContext.Request.Url.Host);
        }

        public static string VerifyPackage(this UrlHelper url)
        {
            return url.Action(actionName: "VerifyPackage", controllerName: "Packages");
        }

        public static string CancelUpload(this UrlHelper url)
        {
            return url.Action(actionName: "CancelUpload", controllerName: "Packages");
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
