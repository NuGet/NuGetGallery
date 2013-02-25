using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;

namespace NuGetGallery.FunctionTests.Helpers
{
    public class UrlHelper
    {     
        private static string _baseurl;
        /// <summary>
        /// The environment against which the test has to be run. The value would be picked from env variable.
        /// If nothing is specified, preview is used as default.
        /// </summary>
        public static string BaseUrl
        {
            get
            {
                if (string.IsNullOrEmpty(_baseurl))
                {
                    if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GalleryUrl")))
                        _baseurl = "https://preview.nuget.org/";
                    else
                        _baseurl = Environment.GetEnvironmentVariable("GalleryUrl");
                }

                return _baseurl;
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

        public static string GetPackagePageUrl(string packageId)
        {
            return UrlHelper.BaseUrl + @"Packages/" + packageId + "/";
        }

        public static string Windows8CuratedFeedUrl
        {
            get { return  UrlHelper.V2FeedRootUrl + Windows8CuratedFeedUrlSuffix; }
        } 

        #region UrlSuffix
        private const string LogonPageUrlSuffix = "/users/account/LogOn";
        private const string LogonPageUrlOnPackageUploadSuffix = "Users/Account/LogOn?ReturnUrl=%2fpackages%2fupload";
        private const string RegisterPageUrlSuffix = "account/Register";
        private const string RegistrationPendingPageUrlSuffix = "account/Thanks";
        private const string StatsPageUrlSuffix = "stats";
        private const string UploadPageUrlSuffix = "/packages/Upload";
        private const string VerifyUploadPageUrlSuffix = "/packages/verify-upload";
        private const string Windows8CuratedFeedUrlSuffix = "/curated-feeds/windows8-packages/";

     

        #endregion UrlSuffix
    }
}


