// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using NuGetGallery.Areas.Admin;
using NuGetGallery.Configuration;
using NuGetGallery.Helpers;

namespace NuGetGallery
{
    public static class UrlExtensions
    {
        private const string Area = "area";
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
            return GetRouteLink(
                url,
                RouteName.StatisticsPackages,
                relativeUrl);
        }

        public static string StatisticsAllPackageVersionDownloads(this UrlHelper url, bool relativeUrl = true)
        {
            return GetRouteLink(
                url,
                RouteName.StatisticsPackageVersions,
                relativeUrl);
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

        /// <summary>
        /// Initializes a package registration link that can be resolved at a later time.
        /// 
        /// Callers should only use this API if they need to generate many links, such as the ManagePackages view
        /// does. This template reduces the calls to RouteCollection.GetVirtualPath which can be expensive. Callers
        /// that only need a single link should call Url.Package instead.
        public static RouteUrlTemplate<IPackageVersionModel> PackageRegistrationTemplate(
            this UrlHelper url,
            bool relativeUrl = true)
        {
            var routesGenerator = new Dictionary<string, Func<IPackageVersionModel, object>>
            {
                { "id", p => p.Id },
                { "version", p => null }
            };

            Func<RouteValueDictionary, string> linkGenerator = rv => GetRouteLink(
                url,
                RouteName.DisplayPackage,
                relativeUrl,
                routeValues: rv);

            return new RouteUrlTemplate<IPackageVersionModel>(linkGenerator, routesGenerator);
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

        public static string LogOnNuGetAccount(this UrlHelper url, bool relativeUrl = true)
        {
            return GetRouteLink(
                url,
                RouteName.Authentication,
                relativeUrl,
                routeValues: new RouteValueDictionary
                {
                    { "action", "LogOnNuGetAccount" }
                });
        }

        public static string LogOnNuGetAccount(this UrlHelper url, string returnUrl, bool relativeUrl = true)
        {
            return GetRouteLink(
                url,
                RouteName.Authentication,
                relativeUrl,
                routeValues: new RouteValueDictionary
                {
                    { "action", "LogOnNuGetAccount" },
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
            if (string.Equals(url.RequestContext.RouteData.DataTokens[Area].ToStringOrNull(), AdminAreaRegistration.Name, StringComparison.OrdinalIgnoreCase))
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
                    { "returnUrl", returnUrl }
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

        /// <summary>
        /// Initializes a user link that can be resolved at a later time.
        /// 
        /// Callers should only use this API if they need to generate many links, such as the ManagePackages view
        /// does. This template reduces the calls to RouteCollection.GetVirtualPath which can be expensive. Callers
        /// that only need a single link should call Url.User instead.
        public static RouteUrlTemplate<User> UserTemplate(
            this UrlHelper url,
            string scheme = null,
            bool relativeUrl = true)
        {
            var routesGenerator = new Dictionary<string, Func<User, object>>
            {
                { "username", u => u.Username }
            };

            Func<RouteValueDictionary, string> linkGenerator = rv => GetActionLink(
                url,
                "Profiles",
                "Users",
                relativeUrl,
                routeValues: rv);

            return new RouteUrlTemplate<User>(linkGenerator, routesGenerator);
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
                routeValues:
                new RouteValueDictionary
                {
                    { "username", username },
                });
        }

        /// <summary>
        /// Initializes an edit package link that can be resolved at a later time.
        /// 
        /// Callers should only use this API if they need to generate many links, such as the ManagePackages view
        /// does. This template reduces the calls to RouteCollection.GetVirtualPath which can be expensive. Callers
        /// that only need a single link should call Url.EditPackage instead.
        public static RouteUrlTemplate<IPackageVersionModel> EditPackageTemplate(
            this UrlHelper url,
            bool relativeUrl = true)
        {
            var routesGenerator = new Dictionary<string, Func<IPackageVersionModel, object>>
            {
                { "action", p => "Edit" },
                { "id", p => p.Id },
                { "version", p => p.Version }
            };

            Func<RouteValueDictionary, string> linkGenerator = rv => GetRouteLink(
                url,
                RouteName.PackageVersionAction,
                relativeUrl,
                routeValues: rv);

            return new RouteUrlTemplate<IPackageVersionModel>(linkGenerator, routesGenerator);
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
                nameof(PackagesController.Reflow),
                "Packages",
                relativeUrl,
                routeValues: new RouteValueDictionary
                {
                    { "id", package.Id },
                    { "version", package.Version }
                });
        }

        public static string RevalidatePackage(
            this UrlHelper url,
            string id,
            string version,
            bool relativeUrl = true)
        {
            return GetActionLink(
                url,
                nameof(PackagesController.Revalidate),
                "Packages",
                relativeUrl,
                routeValues: new RouteValueDictionary
                {
                    { "id", id },
                    { "version", version }
                });
        }

        public static string RevalidatePackage(
            this UrlHelper url,
            IPackageVersionModel package,
            bool relativeUrl = true)
        {
            return url.RevalidatePackage(package.Id, package.Version, relativeUrl);
        }

        /// <summary>
        /// Initializes a delete package link that can be resolved at a later time.
        /// 
        /// Callers should only use this API if they need to generate many links, such as the ManagePackages view
        /// does. This template reduces the calls to RouteCollection.GetVirtualPath which can be expensive. Callers
        /// that only need a single link should call Url.DeletePackage instead.
        public static RouteUrlTemplate<IPackageVersionModel> DeletePackageTemplate(
            this UrlHelper url,
            bool relativeUrl = true)
        {
            var routesGenerator = new Dictionary<string, Func<IPackageVersionModel, object>>
            {
                { "id", p => p.Id },
                { "version", p => p.Version }
            };

            Func<RouteValueDictionary, string> linkGenerator = rv => GetActionLink(
                url,
                "Delete",
                "Packages",
                relativeUrl,
                routeValues: rv);

            return new RouteUrlTemplate<IPackageVersionModel>(linkGenerator, routesGenerator);
        }

        public static string DeletePackage(
            this UrlHelper url,
            IPackageVersionModel package,
            bool relativeUrl = true)
        {
            return url.DeletePackageTemplate(relativeUrl).Resolve(package);
        }

        public static string AccountSettings(
            this UrlHelper url,
            bool relativeUrl = true)
        {
            return GetActionLink(url, "Account", "Users", relativeUrl);
        }

        public static string AdminDeleteAccount(
            this UrlHelper url,
            string accountName,
            bool relativeUrl = true)
        {
            return GetActionLink(url,
                nameof(UsersController.Delete), 
                "Users",
                relativeUrl,
                routeValues: new RouteValueDictionary
                {
                    { "accountName", accountName }
                });
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
                },
                interceptReturnUrl: false);
        }

        public static string ManageMyApiKeys(this UrlHelper url, bool relativeUrl = true)
        {
            return GetActionLink(url, "ApiKeys", "Users", relativeUrl);
        }

        public static string ManageMyOrganizations(this UrlHelper url, bool relativeUrl = true)
        {
            return GetActionLink(url, "Organizations", "Users", relativeUrl);
        }

        public static string ManageMyPackages(this UrlHelper url, bool relativeUrl = true)
        {
            return GetActionLink(url, "Packages", "Users", relativeUrl);
        }

        /// <summary>
        /// Initializes a manage package owners link that can be resolved at a later time.
        /// 
        /// Callers should only use this API if they need to generate many links, such as the ManagePackages view
        /// does. This template reduces the calls to RouteCollection.GetVirtualPath which can be expensive. Callers
        /// that only need a single link should call Url.ManagePackageOwners instead.
        public static RouteUrlTemplate<IPackageVersionModel> ManagePackageOwnersTemplate(this UrlHelper url, bool relativeUrl = true)
        {
            var routesGenerator = new Dictionary<string, Func<IPackageVersionModel, object>>
            {
                { "id", p => p.Id },
            };

            Func<RouteValueDictionary, string> linkGenerator = rv => GetActionLink(
                url,
                "ManagePackageOwners",
                "Packages",
                relativeUrl,
                routeValues: rv);

            return new RouteUrlTemplate<IPackageVersionModel>(linkGenerator, routesGenerator);
        }

        public static string ManagePackageOwners(this UrlHelper url, IPackageVersionModel package, bool relativeUrl = true)
        {
            return url.ManagePackageOwnersTemplate(relativeUrl).Resolve(package);
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

        public static string ConfirmPendingOwnershipRequest(
            this UrlHelper url,
            string packageId,
            string username,
            string confirmationCode,
            bool relativeUrl = true)
        {
            var routeValues = new RouteValueDictionary
            {
                ["id"] = packageId,
                ["username"] = username,
                ["token"] = confirmationCode
            };

            return GetActionLink(url, "ConfirmPendingOwnershipRequest", "Packages", relativeUrl, routeValues);
        }

        public static string RejectPendingOwnershipRequest(
            this UrlHelper url,
            string packageId,
            string username,
            string confirmationCode,
            bool relativeUrl = true)
        {
            var routeValues = new RouteValueDictionary
            {
                ["id"] = packageId,
                ["username"] = username,
                ["token"] = confirmationCode
            };

            return GetActionLink(url, "RejectPendingOwnershipRequest", "Packages", relativeUrl, routeValues);
        }

        public static string CancelPendingOwnershipRequest(
            this UrlHelper url,
            string packageId,
            string requestingUsername,
            string pendingUsername,
            bool relativeUrl = true)
        {
            var routeValues = new RouteValueDictionary
            {
                ["id"] = packageId,
                ["requestingUsername"] = requestingUsername,
                ["pendingUsername"] = pendingUsername
            };

            return GetActionLink(url, "CancelPendingOwnershipRequest", "Packages", relativeUrl, routeValues);
        }

        public static string ConfirmEmail(
            this UrlHelper url,
            string username,
            string token,
            bool relativeUrl = true)
        {
            var routeValues = new RouteValueDictionary
            {
                ["username"] = username,
                ["token"] = token
            };

            return GetActionLink(url, "Confirm", "Users", relativeUrl, routeValues);
        }

        public static string ResetEmailOrPassword(
            this UrlHelper url,
            string username,
            string token,
            bool forgotPassword,
            bool relativeUrl = true)
        {
            var routeValues = new RouteValueDictionary
            {
                ["username"] = username,
                ["token"] = token,
                ["forgot"] = forgotPassword
            };

            return GetActionLink(url, "ResetPassword", "Users", relativeUrl, routeValues);
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
            if (!String.IsNullOrEmpty(_configuration.Current.ExternalTermsOfUseUrl))
            {
                return _configuration.Current.ExternalTermsOfUseUrl;
            }

            return GetActionLink(url, "Terms", "Pages", relativeUrl);
        }

        public static string Privacy(this UrlHelper url, bool relativeUrl = true)
        {
            if (!String.IsNullOrEmpty(_configuration.Current.ExternalPrivacyPolicyUrl))
            {
                return _configuration.Current.ExternalPrivacyPolicyUrl;
            }

            return GetActionLink(url, "Privacy", "Pages", relativeUrl);
        }

        public static string About(this UrlHelper url, bool relativeUrl = true)
        {
            if (!String.IsNullOrEmpty(_configuration.Current.ExternalAboutUrl))
            {
                return _configuration.Current.ExternalAboutUrl;
            }

            return GetActionLink(url, "About", "Pages", relativeUrl);
        }

        public static string Admin(this UrlHelper url, bool relativeUrl = true)
        {
            return GetActionLink(
                url,
                "Index",
                "Home",
                relativeUrl,
                area: AdminAreaRegistration.Name);
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

        public static string RemoveCredential(this UrlHelper url, bool relativeUrl = true)
        {
            return GetActionLink(url, "RemoveCredential", "Users", relativeUrl);
        }

        public static string RegenerateCredential(this UrlHelper url, bool relativeUrl = true)
        {
            return GetActionLink(url, "RegenerateCredential", "Users", relativeUrl);
        }

        public static string EditCredential(this UrlHelper url, bool relativeUrl = true)
        {
            return GetActionLink(url, "EditCredential", "Users", relativeUrl);
        }

        public static string GenerateApiKey(this UrlHelper url, bool relativeUrl = true)
        {
            return GetActionLink(url, "GenerateApiKey", "Users", relativeUrl);
        }

        private static UriBuilder GetCanonicalUrl(UrlHelper url)
        {
            var builder = new UriBuilder(url.RequestContext.HttpContext.Request.Url);
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

        public static string GetActionLink(
            UrlHelper url,
            string actionName,
            string controllerName,
            bool relativeUrl,
            RouteValueDictionary routeValues = null,
            bool interceptReturnUrl = true,
            string area = "" // Default to no area. Admin links should specify the "Admin" area explicitly.
            )
        {
            var protocol = GetProtocol(url);
            var hostName = GetConfiguredSiteHostName();

            routeValues = routeValues ?? new RouteValueDictionary();
            if (!routeValues.ContainsKey(Area))
            {
                routeValues[Area] = area;
            }
            
            if (interceptReturnUrl && routeValues != null && routeValues.ContainsKey("ReturnUrl"))
            {
                routeValues["ReturnUrl"] = GetAbsoluteReturnUrl(
                    routeValues["ReturnUrl"]?.ToString(),
                    protocol,
                    hostName);
            }

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

        internal static string GetAbsoluteReturnUrl(
            string returnUrl,
            string protocol,
            string configuredSiteRootHostName)
        {
            // Ensure return URL is always pointing to the configured siteroot
            // to avoid MVC routing to use the deployment host name instead of the configured one.
            // This is important when deployed behind a proxy, such as APIM.
            if (returnUrl != null 
                && Uri.TryCreate(returnUrl, UriKind.RelativeOrAbsolute, out var returnUri))
            {
                if (!returnUri.IsAbsoluteUri)
                {
                    var baseUri = new Uri($"{protocol}://{configuredSiteRootHostName}");
                    returnUri = new Uri(baseUri, returnUri);
                }

                var uriBuilder = new UriBuilder(returnUri);
                uriBuilder.Host = configuredSiteRootHostName;
                uriBuilder.Port = returnUri.IsDefaultPort ? -1 : returnUri.Port;

                if (string.IsNullOrEmpty(uriBuilder.Query))
                {
                    return uriBuilder.ToString().TrimEnd('/');
                }

                return uriBuilder.ToString();
            }

            // This only happens when the returnUrl did not have a valid Uri format,
            // so we can safely strip this value.
            return string.Empty;
        }
    }
}
