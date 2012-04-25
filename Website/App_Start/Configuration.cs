using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Web;
using Microsoft.WindowsAzure.ServiceRuntime;

namespace NuGetGallery
{
    public class Configuration : IConfiguration
    {
        private static readonly Dictionary<string, Lazy<object>> configThunks = new Dictionary<string, Lazy<object>>();
        private readonly Lazy<string> _httpSiteRootThunk;
        private readonly Lazy<string> _httpsSiteRootThunk;
        static readonly Lazy<bool> runningInAzure = new Lazy<bool>(RunningInAzure);

        public Configuration()
        {
            _httpSiteRootThunk = new Lazy<string>(GetHttpSiteRoot);
            _httpsSiteRootThunk = new Lazy<string>(GetHttpsSiteRoot);
        }

        public static string ReadAppSetting(string key)
        {
            var value = ConfigurationManager.AppSettings[key];
            return value;
        }

        public static string ReadAzureSetting(string key)
        {
            return RoleEnvironment.GetConfigurationSettingValue(key);
        }

        public static string ReadConfiguration(string key)
        {
            return ReadConfiguration<string>(
                key,
                value => value);
        }

        public static T ReadConfiguration<T>(
            string key,
            Func<string, T> valueThunk)
        {
            if (!configThunks.ContainsKey(key))
                configThunks.Add(key, new Lazy<object>(() =>
                {
                    string value = null;

                    if (runningInAzure.Value)
                        value = ReadAzureSetting(key);
                    
                    if (value == null)
                        value = ReadAppSetting(key);
                    
                    return valueThunk(value);
                }));

            return (T)configThunks[key].Value;
        }

        static bool RunningInAzure()
        {
            try
            {
                return RoleEnvironment.IsAvailable;
            }
            catch (RoleEnvironmentException) { }
            catch (TypeInitializationException) { }

            return false;
        }

        public string AzureStorageAccessKey
        {
            get
            {
                return ReadConfiguration("AzureStorageAccessKey");
            }
        }

        public string AzureStorageAccountName
        {
            get
            {
                return ReadConfiguration("AzureStorageAccountName");
            }
        }

        public string AzureStorageBlobUrl
        {
            get
            {
                return ReadConfiguration("AzureStorageBlobUrl");
            }
        }

        public string FileStorageDirectory
        {
            get
            {
                return ReadConfiguration<string>(
                    "FileStorageDirectory",
                    (value) => value ?? HttpContext.Current.Server.MapPath("~/App_Data/Files"));
            }
        }

        public PackageStoreType PackageStoreType
        {
            get
            {
                return ReadConfiguration<PackageStoreType>(
                    "PackageStoreType",
                    (value) => (PackageStoreType)Enum.Parse(typeof(PackageStoreType), value ?? PackageStoreType.NotSpecified.ToString()));
            }
        }

        protected virtual string GetConfiguredSiteRoot()
        {
            return ConfigurationManager.AppSettings["Configuration:SiteRoot"];
        }

        protected virtual HttpRequestBase GetCurrentRequest()
        {
            return new HttpRequestWrapper(HttpContext.Current.Request);
        }

        public string GetSiteRoot(bool useHttps)
        {
            return useHttps ? _httpsSiteRootThunk.Value : _httpSiteRootThunk.Value;
        }
        
        private string GetHttpSiteRoot()
        {
            var request = GetCurrentRequest();
            string siteRoot;
            
            if (request.IsLocal)
                siteRoot = request.Url.GetLeftPart(UriPartial.Authority) + '/';
            else
                siteRoot = GetConfiguredSiteRoot();

            if (!siteRoot.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                && !siteRoot.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The configured site root must start with either http:// or https://.");

            if (siteRoot.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                siteRoot = "http://" + siteRoot.Substring(8);

            return siteRoot;
        }

        private string GetHttpsSiteRoot()
        {
            var siteRoot = _httpSiteRootThunk.Value;

            if (!siteRoot.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The configured HTTP site root must start with http://.");

            return "https://" + siteRoot.Substring(7);
        }
    }
}