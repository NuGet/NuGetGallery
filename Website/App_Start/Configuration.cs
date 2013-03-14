using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Web;
using Microsoft.WindowsAzure.ServiceRuntime;

namespace NuGetGallery
{
    public class Configuration : IConfiguration
    {
        private static readonly Dictionary<string, Func<object>> ConfigThunks = new Dictionary<string, Func<object>>();
        private readonly Lazy<string> _httpSiteRootThunk;
        private readonly Lazy<string> _httpsSiteRootThunk;

        public Configuration()
        {
            _httpSiteRootThunk = new Lazy<string>(GetHttpSiteRoot);
            _httpsSiteRootThunk = new Lazy<string>(GetHttpsSiteRoot);
        }

        public string EnvironmentName
        {
            get { return ReadAppSettings("Environment") ?? "Development"; }
        }

        /// <summary>
        /// Gets a setting indicating if SSL is required for all operations once logged in.
        /// </summary>
        public bool RequireSSL
        {
            get { return ReadAppSettings("SSL.Required", str => Boolean.Parse(str ?? "false")); }
        }

        /// <summary>
        /// Gets the port used for SSL
        /// </summary>
        public int SSLPort
        {
            get { return ReadAppSettings("SSL.Port", str => Int32.Parse(str ?? "443", CultureInfo.InvariantCulture)); }
        }

        public string AzureStorageConnectionString
        {
            get { return ReadAppSettings("AzureStorageConnectionString"); }
        }

        public string AzureDiagnosticsConnectionString
        {
            get { return ReadAppSettings("AzureDiagnosticsConnectionString"); }
        }

        public string AzureStatisticsConnectionString
        {
            get { return ReadAppSettings("AzureStatisticsConnectionString"); }
        }

        public bool ConfirmEmailAddresses
        {
            get { return String.Equals(ReadAppSettings("ConfirmEmailAddresses"), "true", StringComparison.OrdinalIgnoreCase); }
        }

        public bool ReadOnlyMode
        {
            get { return String.Equals(ReadAppSettings("ReadOnlyMode"), "true", StringComparison.OrdinalIgnoreCase); }
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

        public string GalleryOwnerName
        {
            get { return ReadAppSettings("GalleryOwnerName"); }
        }

        public string GalleryOwnerEmail
        {
            get { return ReadAppSettings("GalleryOwnerEmail"); }
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

        public string SmtpHost
        {
            get { return ReadAppSettings("SmtpHost"); }
        }

        public string SmtpUsername
        {
            get { return ReadAppSettings("SmtpUsername"); }
        }

        public string SmtpPassword
        {
            get { return ReadAppSettings("SmtpPassword"); }
        }

        public int? SmtpPort
        {
            get
            {
                string port =  ReadAppSettings("SmtpPort");
                if (String.IsNullOrWhiteSpace(port))
                {
                    return null;
                }

                return Int32.Parse(port, CultureInfo.InvariantCulture);
            }
        }

        public bool UseSmtp
        {
            get { return String.Equals(ReadAppSettings("UseSmtp"), "true", StringComparison.OrdinalIgnoreCase); }
        }

        public string SqlConnectionString
        {
            get
            {
                return ReadConnectionString("NuGetGallery");
            }
        }

        public string AzureCdnHost
        {
            get { return ReadAppSettings("AzureCdnHost"); }
        }

        public string AzureCacheEndpoint
        {
            get { return ReadAppSettings("AzureCacheEndpoint"); }
        }

        public string AzureCacheKey
        {
            get { return ReadAppSettings("AzureCacheKey"); }
        }

        public string GetSiteRoot(bool useHttps)
        {
            return useHttps ? _httpsSiteRootThunk.Value : _httpSiteRootThunk.Value;
        }

        public static string ReadAppSettings(string key)
        {
            return ReadAppSettings(key, value => value);
        }

        public static string ReadConnectionString(string connectionStringName)
        {
            // Read from connection strings and app settings, with app settings winning (to allow us to put the CS in azure config)
            string value = ReadAppSettings("Sql." + connectionStringName);
            return String.IsNullOrEmpty(value) ? ConfigurationManager.ConnectionStrings[connectionStringName].ConnectionString : value;
        }

        public static T ReadAppSettings<T>(
            string key,
            Func<string, T> valueThunk)
        {
            if (!ConfigThunks.ContainsKey(key))
            {
                ConfigThunks.Add(
                    key,
                    // In order to support in-flight changes to config, these need to be Func<T>'s, not Lazy<T>'s, which will cache the value
                    () =>
                    {
                        // Load from config
                        var keyName = String.Format(CultureInfo.InvariantCulture, "Gallery.{0}", key);
                        var value = ConfigurationManager.AppSettings[keyName];

                        // Overwrite from Azure if present
                        if (ContainerBindings.IsDeployedToCloud)
                        {
                            string azureVal;
                            try
                            {
                                azureVal = RoleEnvironment.GetConfigurationSettingValue(keyName);
                            }
                            catch (RoleEnvironmentException)
                            {
                                // Setting does not exist. This is the only way we have to know that... :(
                                azureVal = null;
                            }
                            if (!String.IsNullOrEmpty(azureVal))
                            {
                                value = azureVal;
                            }
                        }

                        // Coalesce empty values to null
                        if (String.IsNullOrWhiteSpace(value))
                        {
                            value = null;
                        }

                        // Pass the value through the "thunk" which parses the string
                        return valueThunk(value);
                    });
            }

            return (T)ConfigThunks[key]();
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

        public static PoliteCaptcha.IConfigurationSource GetPoliteCaptchaConfiguration()
        {
            return new PoliteCaptchaThunk();
        }

        class PoliteCaptchaThunk : PoliteCaptcha.IConfigurationSource
        {
            string PoliteCaptcha.IConfigurationSource.GetConfigurationValue(string key)
            {
                // Fudge the name because Azure cscfg system doesn't allow : in setting names
                return Configuration.ReadAppSettings(key.Replace("::", "."));
            }
        }
    }
}
