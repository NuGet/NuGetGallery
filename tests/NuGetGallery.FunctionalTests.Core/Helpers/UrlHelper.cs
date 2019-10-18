// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery.FunctionalTests
{
    /// <summary>
    /// Provides the Urls to hit various pages in the Gallery WebSite.
    /// </summary>
    public class UrlHelper
    {
        private const string _logonPageUrlSuffix = "users/account/LogOnNuGetAccount";
        private const string _editUrlSuffix = "packages/{0}/{1}/Edit";
        private const string _cancelUrlSuffix = "packages/manage/cancel-upload";
        private const string _signInPageUrlSuffix = "users/account/SignIn";
        private const string _logOffPageUrlSuffix = "users/account/LogOff?returnUrl=%2F";
        private const string _logonPageUrlOnPackageUploadSuffix = "users/account/LogOn?ReturnUrl=%2fpackages%2fupload";
        private const string _packagesPageUrlSuffix = "packages";
        private const string _registerPageUrlSuffix = "account/Register";
        private const string _registrationPendingPageUrlSuffix = "account/Thanks";
        private const string _statsPageUrlSuffix = "stats";
        private const string _aggregateStatsPageUrlSuffix = "/stats/totals";
        private const string _uploadPageUrlSuffix = "packages/manage/Upload";
        private const string _cancelUploadPageUrlSuffix = "packages/manage/cancel-upload";
        private const string _verifyUploadPageUrlSuffix = "/packages/manage/verify-upload";
        private const string _dotnetCuratedFeedUrlSuffix = "curated-feeds/microsoftdotnet/";
        private const string _accountPageUrlSuffix = "/account";
        private const string _accountUnscribeUrlSuffix = "account/subscription/change";
        private const string _accountApiKeyResetUrlSuffix = "/account/GenerateApiKey";
        private const string _manageMyPackagesUrlSuffix = "/account/Packages";
        private const string _aboutPageUrlSuffix = "policies/About";
        private const string _apiStatusUrlSuffix = "api/status";
        private const string _apiGalleryHealthProbeUrlSuffix = "api/health-probe";

        public static string BaseUrl
        {
            get
            {
                return EnsureTrailingSlash(GalleryConfiguration.Instance.GalleryBaseUrl);
            }
        }

        public static string SearchServiceBaseUrl
        {
            get
            {
                return EnsureTrailingSlash(GalleryConfiguration.Instance.SearchServiceBaseUrl);
            }
        }

        public static string V1FeedRootUrl
        {
            get
            {
                return BaseUrl + "api/v1/";
            }
        }

        public static string V2FeedRootUrl
        {
            get
            {
                return BaseUrl + "api/v2/";
            }
        }

        public static string V2FeedPushSourceUrl
        {
            get
            {
                return BaseUrl + "api/v2/package/";
            }
        }


        public static string LogonPageUrl
        {
            get { return BaseUrl + _logonPageUrlSuffix; }
        }

        public static string CancelUrl
        {
            get { return BaseUrl + _cancelUrlSuffix; }
        }

        public static string EditPageUrl
        {
            get { return BaseUrl + _editUrlSuffix; }
        }

        public static string SignInPageUrl
        {
            get { return BaseUrl + _signInPageUrlSuffix; }
        }

        public static string LogOffPageUrl
        {
            get { return BaseUrl + _logOffPageUrlSuffix; }
        }

        public static string PackagesPageUrl
        {
            get { return BaseUrl + _packagesPageUrlSuffix; }
        }

        public static string RegisterPageUrl
        {
            get { return BaseUrl + _registerPageUrlSuffix; }
        }

        public static string RegistrationPendingPageUrl
        {
            get { return BaseUrl + _registrationPendingPageUrlSuffix; }
        }

        public static string StatsPageUrl
        {
            get { return BaseUrl + _statsPageUrlSuffix; }
        }

        public static string AggregateStatsPageUrl
        {
            get { return BaseUrl + _aggregateStatsPageUrlSuffix; }
        }

        public static string UploadPageUrl
        {
            get { return BaseUrl + _uploadPageUrlSuffix; }
        }

        public static string CancelUpload
        {
            get { return BaseUrl + _cancelUploadPageUrlSuffix; }
        }

        public static string VerifyUploadPageUrl
        {
            get { return BaseUrl + _verifyUploadPageUrlSuffix; }
        }

        public static string LogonPageUrlOnPackageUpload
        {
            get { return BaseUrl + _logonPageUrlOnPackageUploadSuffix; }
        }

        public static string DotnetCuratedFeedUrl
        {
            get { return V2FeedRootUrl + _dotnetCuratedFeedUrlSuffix; }
        }

        public static string AccountPageUrl
        {
            get { return BaseUrl + _accountPageUrlSuffix; }
        }

        public static string AccountUnscribeUrl
        {
            get { return BaseUrl + _accountUnscribeUrlSuffix; }
        }

        public static string AccountApiKeyResetUrl
        {
            get { return BaseUrl + _accountApiKeyResetUrlSuffix; }
        }

        public static string ManageMyPackagesUrl
        {
            get { return BaseUrl + _manageMyPackagesUrlSuffix; }
        }

        public static string AboutGalleryPageUrl
        {
            get { return BaseUrl + _aboutPageUrlSuffix; }
        }

        public static string ApiStatusPageUrl => BaseUrl + _apiStatusUrlSuffix;

        public static string ApiGalleryHealthProbeUrl => BaseUrl + _apiGalleryHealthProbeUrlSuffix;

        public static string GetPackagePageUrl(string packageId)
        {
            return GetPackagePageUrl(packageId, "1.0.0");
        }

        public static string GetPackagePageUrl(string packageId, string version)
        {
            return BaseUrl + @"Packages/" + packageId + "/" + version;
        }

        public static string GetPackageDeletePageUrl(string packageId, string version = "1.0.0")
        {
            return BaseUrl + @"Packages/" + packageId + "/" + version + "/Delete";
        }

        public static string GetContactOwnerPageUrl(string packageId)
        {
            return BaseUrl + @"Packages/" + packageId + "/ContactOwners";
        }

        public static string GetAvatarUrl(string accountName)
        {
            return BaseUrl + $"profiles/{accountName}/avatar";
        }

        private static string EnsureTrailingSlash(string siteRoot)
        {
            if (siteRoot == null)
            {
                return "/";
            }

            if (!siteRoot.EndsWith("/", StringComparison.Ordinal))
            {
                siteRoot = string.Format("{0}/", siteRoot);
            }

            return siteRoot;
        }
    }
}



