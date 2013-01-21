using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Web;

namespace NuGetGallery
{
    public class Configuration : IConfiguration
    {
        private static readonly Dictionary<string, Lazy<object>> ConfigThunks = new Dictionary<string, Lazy<object>>();
        private readonly Lazy<string> _httpSiteRootThunk;
        private readonly Lazy<string> _httpsSiteRootThunk;

        public Configuration()
        {
            _httpSiteRootThunk = new Lazy<string>(GetHttpSiteRoot);
            _httpsSiteRootThunk = new Lazy<string>(GetHttpsSiteRoot);
        }

        public string AzureStorageAccessKey
        {
            get { return ReadAppSettings("AzureStorageAccessKey"); }
        }

        public string AzureStorageAccountName
        {
            get { return ReadAppSettings("AzureStorageAccountName"); }
        }

        public string AzureStorageBlobUrl
        {
            get { return ReadAppSettings("AzureStorageBlobUrl"); }
        }

        public string AzureStatisticsConnectionString
        {
            get { return ReadConnectionString("AzureStatistics"); }
        }

        public bool UseEmulator
        {
            get { return String.Equals(ReadAppSettings("UseAzureEmulator"), "true", StringComparison.OrdinalIgnoreCase); }
        }

        public string FileStorageDirectory
        {
            get
            {
                return ReadAppSettings(
                    "FileStorageDirectory",
                    value => value ?? HttpContext.Current.Server.MapPath("~/App_Data/Files"));
            }
        }

        public PackageStoreType PackageStoreType
        {
            get
            {
                return ReadAppSettings(
                    "PackageStoreType",
                    value => (PackageStoreType)Enum.Parse(typeof(PackageStoreType), value ?? PackageStoreType.NotSpecified.ToString()));
            }
        }

        public string AzureCdnHost
        {
            get { return ReadAppSettings("AzureCdnHost"); }
        }

        public string GetSiteRoot(bool useHttps)
        {
            return useHttps ? _httpsSiteRootThunk.Value : _httpSiteRootThunk.Value;
        }

        public static string ReadAppSettings(string key)
        {
            return ReadAppSettings(key, value => value);
        }

        public static T ReadAppSettings<T>(
            string key,
            Func<string, T> valueThunk)
        {
            if (!ConfigThunks.ContainsKey(key))
            {
                ConfigThunks.Add(
                    key,
                    new Lazy<object>(
                        () =>
                            {
                                var value = ConfigurationManager.AppSettings[String.Format(CultureInfo.InvariantCulture, "Gallery:{0}", key)];
                                if (String.IsNullOrWhiteSpace(value))
                                {
                                    value = null;
                                }
                                return valueThunk(value);
                            }));
            }

            return (T)ConfigThunks[key].Value;
        }

        public static string ReadConnectionString(string key)
        {
            return ReadConnectionString(key, value => value);
        }

        public static string ReadConnectionString(string key, Func<string, string> valueThunk)
        {
            if (!ConfigThunks.ContainsKey(key))
            {
                ConfigThunks.Add(
                    key,
                    new Lazy<object>(
                        () =>
                        {
                            foreach (ConnectionStringSettings settings in ConfigurationManager.ConnectionStrings)
                            {
                                if (settings.Name == key)
                                {
                                    return valueThunk(settings.ConnectionString);
                                }
                            }
                            return valueThunk(null);
                        }));
            }

            return (string)ConfigThunks[key].Value;
        }

        public string FacebookAppID
        {
            get
            {
                return ReadAppSettings("FacebookAppId");
            }
        }

        protected virtual string GetConfiguredSiteRoot()
        {
            return ReadAppSettings("SiteRoot");
        }

        protected virtual HttpRequestBase GetCurrentRequest()
        {
            return new HttpRequestWrapper(HttpContext.Current.Request);
        }

        private string GetHttpSiteRoot()
        {
            var request = GetCurrentRequest();
            string siteRoot;

            if (request.IsLocal)
            {
                siteRoot = request.Url.GetLeftPart(UriPartial.Authority) + '/';
            }
            else
            {
                siteRoot = GetConfiguredSiteRoot();
            }

            if (!siteRoot.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                && !siteRoot.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The configured site root must start with either http:// or https://.");
            }

            if (siteRoot.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                siteRoot = "http://" + siteRoot.Substring(8);
            }

            return siteRoot;
        }

        private string GetHttpsSiteRoot()
        {
            var siteRoot = _httpSiteRootThunk.Value;

            if (!siteRoot.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The configured HTTP site root must start with http://.");
            }

            return "https://" + siteRoot.Substring(7);
        }
    }
}