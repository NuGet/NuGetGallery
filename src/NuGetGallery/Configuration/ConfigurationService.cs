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
using NuGet.Services.KeyVault;
using NuGetGallery.Configuration.SecretReader;

namespace NuGetGallery.Configuration
{
    public class ConfigurationService : PoliteCaptcha.IConfigurationSource, IGalleryConfigurationService
    {
        protected const string SettingPrefix = "Gallery.";
        protected const string FeaturePrefix = "Feature.";
        private bool _notInCloud;
        private ISecretReaderFactory _secretReaderFactory;
        private Lazy<ISecretInjector> _secretInjector;
        private string _httpSiteRoot;
        private string _httpsSiteRoot;

        protected IAppConfiguration _appConfig;
        protected FeatureConfiguration _featuresConfig;

        public ConfigurationService(ISecretReaderFactory secretReaderFactory)
        {
            if (secretReaderFactory == null)
            {
                throw new ArgumentNullException(nameof(secretReaderFactory));
            }

            _secretReaderFactory = secretReaderFactory;
            _secretInjector = new Lazy<ISecretInjector>(InitSecretInjector, isThreadSafe: false);
        }

        public static IEnumerable<PropertyDescriptor> GetConfigProperties<T>(T instance)
        {
            return TypeDescriptor.GetProperties(instance).Cast<PropertyDescriptor>().Where(p => !p.IsReadOnly);
        }

        /// <summary>
        /// PoliteCaptcha.IConfigurationSource implementation
        /// </summary>
        public string GetConfigurationValue(string key)
        {
            // Fudge the name because Azure cscfg system doesn't allow : in setting names
            // Used by PoliteCaptcha
            return ReadSetting(key.Replace("::", ".")).Result;
        }

        /// <summary>
        /// Asynchronously access current configuration.
        /// </summary>
        /// <returns>The current configuration.</returns>
        public virtual async Task<IAppConfiguration> GetCurrent()
        {
            return _appConfig = await ResolveSettings();
        }

        /// <summary>
        /// Asynchronously access features configuration.
        /// </summary>
        /// <returns>The features configuration.</returns>
        public virtual async Task<FeatureConfiguration> GetFeatures()
        {
            return _featuresConfig = await ResolveFeatures();
        }

        /// <summary>
        /// Synchronously access current configuration in contexts that cannot be async.
        /// Avoid accessing configuration that changes with this method (e.g. Azure connection strings).
        /// </summary>
        /// <returns>The cached current configuration.</returns>
        [Obsolete("Use GetCurrent() unless a synchronous context is completely necessary.")]
        public virtual IAppConfiguration Current => _appConfig ?? GetCurrent().Result;

        /// <summary>
        /// Synchronously access features configuration in contexts that cannot be async.
        /// Avoid accessing configuration that changes with this method (e.g. Azure connection strings).
        /// </summary>
        /// <returns>The cached features configuration.</returns>
        [Obsolete("Use GetFeatures() unless a synchronous context is completely necessary.")]
        public virtual FeatureConfiguration Features => _featuresConfig ?? GetFeatures().Result;

        /// <summary>
        /// Gets the site root using the specified protocol
        /// </summary>
        /// <param name="useHttps">If true, the root will be returned in HTTPS form, otherwise, HTTP.</param>
        /// <returns></returns>
        public string GetSiteRoot(bool useHttps)
        {
            // It is ok to call Result on these methods because initialization of the SiteRoot variables
            // should occur early in initialization before any deadlock is possible.
            if (useHttps && _httpsSiteRoot == null)
            {
                _httpsSiteRoot = GetHttpsSiteRoot().Result;
            }
            if (_httpSiteRoot == null)
            {
                _httpSiteRoot = GetHttpSiteRoot().Result;
            }

            return (useHttps ? _httpsSiteRoot : _httpSiteRoot);
        }

        public async Task<T> ResolveConfigObject<T>(T instance, string prefix)
        {
            // Iterate over the properties
            foreach (var property in GetConfigProperties<T>(instance))
            {
                // Try to get a config setting value
                string baseName = string.IsNullOrEmpty(property.DisplayName) ? property.Name : property.DisplayName;
                string settingName = prefix + baseName;

                string value = await ReadSetting(settingName);

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
        
        public async Task<string> ReadSetting(string settingName)
        {
            string value;

            value = GetCloudSetting(settingName);

            if (value == "null")
            {
                value = null;
            }
            else if (string.IsNullOrEmpty(value))
            {
                var cstr = GetConnectionString(settingName);
                value = cstr != null ? cstr.ConnectionString : GetAppSetting(settingName);
            }

            if (!string.IsNullOrEmpty(value))
            {
                value = await _secretInjector.Value.InjectAsync(value);
            }

            return value;
        }

        protected virtual HttpRequestBase GetCurrentRequest()
        {
            return new HttpRequestWrapper(HttpContext.Current.Request);
        }


        private ISecretInjector InitSecretInjector()
        {
            return _secretReaderFactory.CreateSecretInjector(_secretReaderFactory.CreateSecretReader(new ConfigurationService(new EmptySecretReaderFactory())));
        }

        private async Task<FeatureConfiguration> ResolveFeatures()
        {
            return await ResolveConfigObject(new FeatureConfiguration(), FeaturePrefix);
        }

        private async Task<IAppConfiguration> ResolveSettings()
        {
            return await ResolveConfigObject(new AppConfiguration(), SettingPrefix);
        }

        protected virtual string GetCloudSetting(string settingName)
        {
            // Short-circuit if we've already determined we're not in the cloud
            if (_notInCloud)
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
                    _notInCloud = true;
                }
            }
            catch (TypeInitializationException)
            {
                // Not in the role environment...
                _notInCloud = true; // Skip future checks to save perf
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
      
        private async Task<string> GetHttpSiteRoot()
        {
            var request = GetCurrentRequest();
            string siteRoot;

            if (request.IsLocal)
            {
                siteRoot = request.Url.GetLeftPart(UriPartial.Authority) + '/';
            }
            else
            {
                siteRoot = (await GetCurrent()).SiteRoot;
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

        private async Task<string> GetHttpsSiteRoot()
        {
            var siteRoot = _httpSiteRoot;

            if (string.IsNullOrEmpty(siteRoot))
            {
                siteRoot = _httpSiteRoot = await GetHttpSiteRoot();
            }

            if (!siteRoot.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The configured HTTP site root must start with http://.");
            }

            return "https://" + siteRoot.Substring(7);
        }
    }
}