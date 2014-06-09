using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;

namespace NuGetGallery.FunctionTests.Helpers
{
    /// <summary>
    /// Provides the Urls to hit various pages in the Gallery WebSite.
    /// </summary>
    public class UrlHelper
    {
        public static string BaseUrl
        {
            get
            {
                return EnvironmentSettings.BaseUrl;
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
            get { return UrlHelper.BaseUrl + LogonPageUrlSuffix; }
        }

        public static string CancelUrl
        {
            get { return UrlHelper.BaseUrl + CancelUrlSuffix; }
        }

        public static string EditPageUrl
        {
            get { return UrlHelper.BaseUrl + EditUrlSuffix; }
        }

        public static string SignInPageUrl
        {
            get { return UrlHelper.BaseUrl + SignInPageUrlSuffix; }
        }

        public static string LogOffPageUrl
        {
            get { return UrlHelper.BaseUrl + LogOffPageUrlSuffix; }
        }

        public static string PackagesPageUrl
        {
            get { return UrlHelper.BaseUrl + PackagesPageUrlSuffix; }
        }

        public static string RegisterPageUrl
        {
            get { return UrlHelper.BaseUrl + RegisterPageUrlSuffix; }
        }

        public static string RegistrationPendingPageUrl
        {
            get { return UrlHelper.BaseUrl + RegistrationPendingPageUrlSuffix; }
        }

        public static string StatsPageUrl
        {
            get { return UrlHelper.BaseUrl + StatsPageUrlSuffix; }
        }

        public static string AggregateStatsPageUrl
        {
            get { return UrlHelper.BaseUrl + AggregateStatsPageUrlSuffix; }
        }

        public static string UploadPageUrl
        {
            get { return UrlHelper.BaseUrl + UploadPageUrlSuffix; }
        }

        public static string VerifyUploadPageUrl
        {
            get { return UrlHelper.BaseUrl + VerifyUploadPageUrlSuffix; }
        }

        public static string LogonPageUrlOnPackageUpload
        {
            get { return UrlHelper.BaseUrl + LogonPageUrlOnPackageUploadSuffix; }
        }

        public static string Windows8CuratedFeedUrl
        {
            get { return UrlHelper.V2FeedRootUrl + Windows8CuratedFeedUrlSuffix; }
        }

        public static string WebMatrixCuratedFeedUrl
        {
            get { return UrlHelper.V2FeedRootUrl + WebMatrixCuratedFeedUrlSuffix; }
        }

        public static string DotnetCuratedFeedUrl
        {
            get { return UrlHelper.V2FeedRootUrl + DotnetCuratedFeedUrlSuffix; }
        }

        public static string AccountPageUrl
        {
            get { return UrlHelper.BaseUrl + AccountPageUrlSuffix; }
        }

        public static string AccountUnscribeUrl
        {
            get { return UrlHelper.BaseUrl + AccountUnscribeUrlSuffix; }
        }

        public static string AccountApiKeyResetUrl
        {
            get { return UrlHelper.BaseUrl + AccountApiKeyResetUrlSuffix; }
        } 

        public static string ManageMyPackagesUrl
        {
            get { return UrlHelper.BaseUrl + ManageMyPackagesUrlSuffix; }
        }

        public static string AboutGalleryPageUrl
        {
            get { return UrlHelper.BaseUrl + AboutPageUrlSuffix; }
        }

        public static string GetPackagePageUrl(string packageId)
        {
            return UrlHelper.BaseUrl + @"Packages/" + packageId;
        }

        public static string GetPackagePageUrl(string packageId, string version = "1.0.0")
        {
            return UrlHelper.BaseUrl + @"Packages/" + packageId + "/" + version;
        }

        public static string GetPackageDeletePageUrl(string packageId, string version = "1.0.0")
        {
            return UrlHelper.BaseUrl + @"Packages/" + packageId + "/" + version + "/Delete";
        }

        public static string GetContactOwnerPageUrl(string packageId)
        {
            return UrlHelper.BaseUrl + @"Packages/" + packageId + "/ContactOwners";
        }

        #region UrlSuffix
        private const string LogonPageUrlSuffix = "/users/account/LogOn";
        private const string EditUrlSuffix = "/packages/{0}/{1}/Edit";
        private const string CancelUrlSuffix = "packages/cancel-upload";
        private const string SignInPageUrlSuffix = "/users/account/SignIn";
        private const string LogOffPageUrlSuffix = "/users/account/LogOff?returnUrl=%2F";
        private const string LogonPageUrlOnPackageUploadSuffix = "Users/Account/LogOn?ReturnUrl=%2fpackages%2fupload";
        private const string PackagesPageUrlSuffix = "/packages";
        private const string RegisterPageUrlSuffix = "account/Register";
        private const string RegistrationPendingPageUrlSuffix = "account/Thanks";
        private const string StatsPageUrlSuffix = "stats";
        private const string AggregateStatsPageUrlSuffix = "/stats/totals";
        private const string UploadPageUrlSuffix = "/packages/Upload";
        private const string VerifyUploadPageUrlSuffix = "/packages/verify-upload";
        private const string Windows8CuratedFeedUrlSuffix = "curated-feeds/windows8-packages/";
        private const string WebMatrixCuratedFeedUrlSuffix = "curated-feeds/webmatrix/";
        private const string DotnetCuratedFeedUrlSuffix = "curated-feeds/microsoftdotnet/";
        private const string AccountPageUrlSuffix = "/account";
        private const string AccountUnscribeUrlSuffix = "account/unsubscribe";
        private const string AccountApiKeyResetUrlSuffix = "/account/GenerateApiKey";
        private const string ManageMyPackagesUrlSuffix = "/account/Packages";
        private const string AboutPageUrlSuffix = "policies/About";
        #endregion UrlSuffix
    }
}



