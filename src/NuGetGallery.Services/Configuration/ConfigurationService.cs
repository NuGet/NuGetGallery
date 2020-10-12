// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Configuration;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Configuration;
using Microsoft.WindowsAzure.ServiceRuntime;
using NuGet.Services.Configuration;
using NuGet.Services.KeyVault;
using NuGetGallery.Configuration.SecretReader;

namespace NuGetGallery.Configuration
{
    public class ConfigurationService : IGalleryConfigurationService, IConfigurationFactory
    {
        protected const string SettingPrefix = "Gallery.";
        protected const string FeaturePrefix = "Feature.";
        protected const string ServiceBusPrefix = "AzureServiceBus.";
        protected const string PackageDeletePrefix = "PackageDelete.";

        private bool _notInCloudService;
        private readonly Lazy<string> _httpSiteRootThunk;
        private readonly Lazy<string> _httpsSiteRootThunk;
        private readonly Lazy<IAppConfiguration> _lazyAppConfiguration;
        private readonly Lazy<FeatureConfiguration> _lazyFeatureConfiguration;
        private readonly Lazy<IServiceBusConfiguration> _lazyServiceBusConfiguration;
        private readonly Lazy<IPackageDeleteConfiguration> _lazyPackageDeleteConfiguration;

        private static readonly HashSet<string> NotInjectedSettingNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            SettingPrefix + "SqlServer",
            SettingPrefix + "SqlServerReadOnlyReplica",
            SettingPrefix + "SupportRequestSqlServer",
            SettingPrefix + "ValidationSqlServer" };

        public ISecretInjector SecretInjector { get; set; }

        /// <summary>
        /// Initializes the configuration service and associates a secret injector based on the configured KeyVault
        /// settings.
        /// </summary>
        public static ConfigurationService Initialize()
        {
            var configuration = new ConfigurationService();
            var secretReaderFactory = new SecretReaderFactory(configuration);
            var secretReader = secretReaderFactory.CreateSecretReader();
            var secretInjector = secretReaderFactory.CreateSecretInjector(secretReader);

            configuration.SecretInjector = secretInjector;

            return configuration;
        }

        public ConfigurationService()
        {
            _httpSiteRootThunk = new Lazy<string>(GetHttpSiteRoot);
            _httpsSiteRootThunk = new Lazy<string>(GetHttpsSiteRoot);

            _lazyAppConfiguration = new Lazy<IAppConfiguration>(() => ResolveSettings().Result);
            _lazyFeatureConfiguration = new Lazy<FeatureConfiguration>(() => ResolveFeatures().Result);
            _lazyServiceBusConfiguration = new Lazy<IServiceBusConfiguration>(() => ResolveServiceBus().Result);
            _lazyPackageDeleteConfiguration = new Lazy<IPackageDeleteConfiguration>(() => ResolvePackageDelete().Result);
        }

        public static IEnumerable<PropertyDescriptor> GetConfigProperties<T>(T instance)
        {
            return TypeDescriptor.GetProperties(instance).Cast<PropertyDescriptor>().Where(p => !p.IsReadOnly);
        }

        public IAppConfiguration Current => _lazyAppConfiguration.Value;

        public FeatureConfiguration Features => _lazyFeatureConfiguration.Value;

        public IServiceBusConfiguration ServiceBus => _lazyServiceBusConfiguration.Value;

        public IPackageDeleteConfiguration PackageDelete => _lazyPackageDeleteConfiguration.Value;

        /// <summary>
        /// Gets the site root using the specified protocol
        /// </summary>
        /// <param name="useHttps">If true, the root will be returned in HTTPS form, otherwise, HTTP.</param>
        /// <returns></returns>
        public string GetSiteRoot(bool useHttps)
        {
            return useHttps ? _httpsSiteRootThunk.Value : _httpSiteRootThunk.Value;
        }

        /// <summary>
        /// Gets the site domain
        /// </summary>
        public string GetSiteDomain()
        {
            var siteDomain = _httpsSiteRootThunk.Value.Substring(8);
            if (siteDomain.EndsWith("/"))
            {
                return siteDomain.Substring(0, siteDomain.Length - 1);
            }

            return siteDomain;
        }

        public Task<T> Get<T>() where T : NuGet.Services.Configuration.Configuration, new()
        {
            // Get the prefix specified by the ConfigurationKeyPrefixAttribute on the class if it exists.
            var classPrefix = string.Empty;
            var configNamePrefixProperty = (ConfigurationKeyPrefixAttribute)typeof(T)
                .GetCustomAttributes(typeof(ConfigurationKeyPrefixAttribute), inherit: true)
                .FirstOrDefault();
            if (configNamePrefixProperty != null)
            {
                classPrefix = configNamePrefixProperty.Prefix;
            }

            return ResolveConfigObject(Activator.CreateInstance<T>(), classPrefix);
        }

        public async Task<T> ResolveConfigObject<T>(T instance, string prefix)
        {
            // Iterate over the properties
            foreach (var property in GetConfigProperties<T>(instance))
            {
                // Try to get a config setting value
                string baseName = string.IsNullOrEmpty(property.DisplayName) ? property.Name : property.DisplayName;
                string settingName = prefix + baseName;

                string value = await ReadSettingAsync(settingName);

                if (string.IsNullOrEmpty(value))
                {
                    var defaultValue = property.Attributes.OfType<DefaultValueAttribute>().FirstOrDefault();
                    if (defaultValue != null && defaultValue.Value != null)
                    {
                        if (defaultValue.Value.GetType() == property.PropertyType)
                        {
                            property.SetValue(instance, defaultValue.Value);
                            continue;
                        }
                        else
                        {
                            value = defaultValue.Value as string;
                        }
                    }
                }

                if (!string.IsNullOrEmpty(value))
                {
                    if (property.PropertyType.IsAssignableFrom(typeof(string)))
                    {
                        property.SetValue(instance, value);
                    }
                    else if (property.Converter != null && property.Converter.CanConvertFrom(typeof(string)))
                    {
                        // Convert the value
                        property.SetValue(instance, property.Converter.ConvertFromString(value));
                    }
                }
                else if (property.Attributes.OfType<RequiredAttribute>().Any())
                {
                    throw new ConfigurationErrorsException(string.Format(CultureInfo.InvariantCulture, "Missing required configuration setting: '{0}'", settingName));
                }
            }
            return instance;
        }

        public async Task<string> ReadSettingAsync(string settingName)
        {
            var value = ReadRawSetting(settingName);

            if (!string.IsNullOrEmpty(value) && !NotInjectedSettingNames.Contains(settingName))
            {
                value = await SecretInjector.InjectAsync(value);
            }

            return value;
        }

        public string ReadRawSetting(string settingName)
        {
            string value;

            value = GetCloudServiceSetting(settingName);

            if (value == "null")
            {
                value = null;
            }
            else if (string.IsNullOrEmpty(value))
            {
                var cstr = GetConnectionString(settingName);
                value = cstr != null ? cstr.ConnectionString : GetAppSetting(settingName);
            }

            return value;
        }

        protected virtual HttpRequestBase GetCurrentRequest()
        {
            return new HttpRequestWrapper(HttpContext.Current.Request);
        }

        private async Task<FeatureConfiguration> ResolveFeatures()
        {
            return await ResolveConfigObject(new FeatureConfiguration(), FeaturePrefix);
        }

        private async Task<IAppConfiguration> ResolveSettings()
        {
            return await ResolveConfigObject(new AppConfiguration(), SettingPrefix);
        }

        private async Task<IServiceBusConfiguration> ResolveServiceBus()
        {
            return await ResolveConfigObject(new ServiceBusConfiguration(), ServiceBusPrefix);
        }

        private async Task<IPackageDeleteConfiguration> ResolvePackageDelete()
        {
            return await ResolveConfigObject(new PackageDeleteConfiguration(), PackageDeletePrefix);
        }

        protected virtual string GetCloudServiceSetting(string settingName)
        {
            // Short-circuit if we've already determined we're not in the cloud
            if (_notInCloudService)
            {
                return null;
            }

            string value = null;
            try
            {
                if (RoleEnvironment.IsAvailable)
                {
                    value = RoleEnvironment.GetConfigurationSettingValue(settingName);
                }
                else
                {
                    _notInCloudService = true;
                }
            }
            catch (TypeInitializationException)
            {
                // Not in the role environment...
                _notInCloudService = true; // Skip future checks to save perf
            }
            catch (Exception)
            {
                // Value not present
                return null;
            }
            return value;
        }

        protected virtual string GetAppSetting(string settingName)
        {
            return WebConfigurationManager.AppSettings[settingName];
        }

        protected virtual ConnectionStringSettings GetConnectionString(string settingName)
        {
            return WebConfigurationManager.ConnectionStrings[settingName];
        }

        private string GetHttpSiteRoot()
        {
            var siteRoot = Current.SiteRoot;

            if (siteRoot == null)
            {
                // No SiteRoot configured in settings.
                // Fallback to detected site root.
                var request = GetCurrentRequest();
                siteRoot = request.Url.GetLeftPart(UriPartial.Authority) + '/';
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
