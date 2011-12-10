using System;
using System.Collections.Generic;
using System.Configuration;
using System.Web;
using Microsoft.WindowsAzure.ServiceRuntime;

namespace NuGetGallery
{
    internal class Configuration : IConfiguration
    {
        private static readonly Dictionary<string, Lazy<object>> configThunks = new Dictionary<string, Lazy<object>>();
        private readonly Lazy<string> siteRoot = new Lazy<string>(GetSiteRoot); 
        static readonly Lazy<bool> runningInAzure = new Lazy<bool>(RunningInAzure);

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

        public string SiteRoot
        {
            get
            {
                return siteRoot.Value;
            }
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

        private static string GetSiteRoot()
        {
            // TODO: Make this less horrid. 
            var request = HttpContext.Current.Request;
            if (request.IsLocal)
            {
                return request.Url.GetLeftPart(UriPartial.Authority) + '/';
            }

            return ConfigurationManager.AppSettings["Configuration:SiteRoot"]; 
        }
    }
}