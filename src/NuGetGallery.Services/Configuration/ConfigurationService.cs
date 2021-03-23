// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Configuration;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.ServiceRuntime;
using NuGet.Services.Configuration;
using NuGet.Services.KeyVault;
using NuGetGallery.Authentication.Providers;
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
            SettingPrefix + "ValidationSqlServer",

            // Telemetry configuration contains no secrets, preventing injection, so we could create one
            // before we set up secret injection.
            SettingPrefix + nameof(ITelemetryConfiguration.AppInsightsHeartbeatIntervalSeconds),
            SettingPrefix + nameof(ITelemetryConfiguration.AppInsightsInstrumentationKey),
            SettingPrefix + nameof(ITelemetryConfiguration.AppInsightsSamplingPercentage),
            SettingPrefix + nameof(ITelemetryConfiguration.DeploymentLabel),
        };

        private readonly Dictionary<ConfigurationType, ConfigurationDescription> _configurationTypes;

        public ISyncSecretInjector SecretInjector { get; set; }

        private enum ConfigurationType
        {
            AppConfiguration,
            FeatureConfiguration,
            ServiceBusConfiguration,
            PackageDeleteConfiguration,
            AuthenticatorConfiguration,
        }

        /// <summary>
        /// Initializes the configuration service and associates a secret injector based on the configured KeyVault
        /// settings.
        /// </summary>
        public static ConfigurationService Initialize(ILoggerFactory loggerFactory, TelemetryService telemetryService)
        {
            var configuration = new ConfigurationService();
            var secretReaderFactory = new SecretReaderFactory(configuration);
            var secretReader = secretReaderFactory.CreateSecretReader(
                configuration.GetAllSecretNames(),
                loggerFactory.CreateLogger("SecretReader"),
                telemetryService);
            var secretInjector = secretReaderFactory.CreateSecretInjector(secretReader);

            configuration.SecretInjector = secretInjector;
            secretReader.WaitForTheFirstRefresh();

            return configuration;
        }

        public static ITelemetryConfiguration GetTelemetryConfiguration()
        {
            var configuration = new ConfigurationService();
            // we expect that all properties of ITelemetryConfiguration are listed in
            // NotInjectedSettingNames above, so we would be able to initialize it without
            // needing to initialize secret injection
            return configuration.ResolveConfigObject(new TelemetryConfiguration(), SettingPrefix);
        }

        public ConfigurationService()
        {
            _httpSiteRootThunk = new Lazy<string>(GetHttpSiteRoot);
            _httpsSiteRootThunk = new Lazy<string>(GetHttpsSiteRoot);

            _lazyAppConfiguration = new Lazy<IAppConfiguration>(() => ResolveSettings());
            _lazyFeatureConfiguration = new Lazy<FeatureConfiguration>(() => ResolveFeatures());
            _lazyServiceBusConfiguration = new Lazy<IServiceBusConfiguration>(() => ResolveServiceBus());
            _lazyPackageDeleteConfiguration = new Lazy<IPackageDeleteConfiguration>(() => ResolvePackageDelete());

            _configurationTypes = new Dictionary<ConfigurationType, ConfigurationDescription>()
            {
                {
                    ConfigurationType.AppConfiguration,
                    new ConfigurationDescription(
                        configService => configService.ResolveConfigObject(new AppConfiguration(), SettingPrefix),
                        configService => configService.GetSecretNames(new AppConfiguration(), SettingPrefix))
                },
                {
                    ConfigurationType.FeatureConfiguration,
                    new ConfigurationDescription(
                        configService => configService.ResolveConfigObject(new FeatureConfiguration(), FeaturePrefix),
                        configService => configService.GetSecretNames(new FeatureConfiguration(), FeaturePrefix))
                },
                {
                    ConfigurationType.ServiceBusConfiguration,
                    new ConfigurationDescription(
                        configService => configService.ResolveConfigObject(new ServiceBusConfiguration(), ServiceBusPrefix),
                        configService => configService.GetSecretNames(new ServiceBusConfiguration(), ServiceBusPrefix))
                },
                {
                    ConfigurationType.PackageDeleteConfiguration,
                    new ConfigurationDescription(
                        configService => configService.ResolveConfigObject(new PackageDeleteConfiguration(), PackageDeletePrefix),
                        configService => configService.GetSecretNames(new PackageDeleteConfiguration(), PackageDeletePrefix))
                },
                {
                    ConfigurationType.AuthenticatorConfiguration,
                    new ConfigurationDescription(
                        configService => throw new NotImplementedException(),
                        configService => configService.GetAuthenticatorSecretNames(Authenticator.AuthPrefix))
                },
            };
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

            return Task.FromResult(ResolveConfigObject(Activator.CreateInstance<T>(), classPrefix));
        }

        public T ResolveConfigObject<T>(T instance, string prefix)
        {
            // Iterate over the properties
            foreach (var property in GetConfigProperties<T>(instance))
            {
                // Try to get a config setting value
                string settingName = GetSettingName(property, prefix);
                string value = ReadSetting(settingName);

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

        public string ReadSetting(string settingName)
        {
            var value = ReadRawSetting(settingName);

            if (!string.IsNullOrEmpty(value) && !NotInjectedSettingNames.Contains(settingName))
            {
                value = SecretInjector.Inject(value);
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

        private static string GetSettingName(PropertyDescriptor property, string prefix)
        {
            string baseName = string.IsNullOrEmpty(property.DisplayName) ? property.Name : property.DisplayName;
            return prefix + baseName;
        }

        private ICollection<string> GetSecretNames<T>(T instance, string prefix)
        {
            var secretNames = new HashSet<string>();
            foreach (var property in GetConfigProperties<T>(instance))
            {
                string settingName = GetSettingName(property, prefix);
                string settingRawValue = ReadRawSetting(settingName);
                if (settingRawValue != null)
                {
                    secretNames.UnionWith(NuGet.Services.KeyVault.SecretUtilities.GetSecretNames(settingRawValue, NuGet.Services.KeyVault.SecretInjector.DefaultFrame));
                }
            }

            return secretNames;
        }

        private class AuthenticatorInfo
        {
            public Type Type { get; set; }
            public Type BaseType { get; set; }
        }

        private ICollection<string> GetAuthenticatorSecretNames(string authPrefix)
        {
            // Authenticator resolves configuration on its own, so we'll mimic its behavior to generate
            // all possible secret names it could reach

            var authenticators = Assembly.GetExecutingAssembly().GetTypes()
                .Select(t => new AuthenticatorInfo { Type = t, BaseType = GetBaseGenericAuthenticator(t) })
                .Where(info => info.BaseType != null)
                .ToList();

            var secretNames = new HashSet<string>();
            foreach (var authenticator in authenticators)
            {
                var prefix = $"{Authenticator.AuthPrefix}{Authenticator.GetName(authenticator.Type)}.";
                var configurationType = authenticator.BaseType.GetGenericArguments()[0];
                secretNames.UnionWith(GetSecretNames(Activator.CreateInstance(configurationType), prefix));
            }

            return secretNames;
        }

        /// <summary>
        /// Checks if the type is concrete and descends from <see cref="Authenticator{TConfig}"/>.
        /// </summary>
        /// <param name="t">Type to check.</param>
        /// <returns>Closed generic type of Authenticator it descends from or null if it doesn't.</returns>
        private static Type GetBaseGenericAuthenticator(Type t)
        {
            bool basicChecks = !t.IsAbstract
                    && !t.IsInterface
                    && t.BaseType != null;
            if (!basicChecks)
            {
                return null;
            }

            var baseType = t.BaseType;

            while (baseType != null)
            {
                if (baseType.IsGenericType && baseType.GetGenericTypeDefinition() == typeof(Authenticator<>))
                {
                    return baseType;
                }
                baseType = baseType.BaseType;
            }

            return null;
        }

        protected virtual HttpRequestBase GetCurrentRequest()
        {
            return new HttpRequestWrapper(HttpContext.Current.Request);
        }

        private class ConfigurationDescription
        {
            public Func<ConfigurationService, object> GetResolvedConfigurationObject { get; }
            public Func<ConfigurationService, ICollection<string>> GetSecretNames { get; }

            public ConfigurationDescription(Func<ConfigurationService, object> getResolvedConfigurationObject, Func<ConfigurationService, ICollection<string>> getSecretNames)
            {
                GetResolvedConfigurationObject = getResolvedConfigurationObject ?? throw new ArgumentNullException(nameof(getResolvedConfigurationObject));
                GetSecretNames = getSecretNames ?? throw new ArgumentNullException(nameof(getSecretNames));
            }
        }

        private FeatureConfiguration ResolveFeatures()
        {
            return (FeatureConfiguration)_configurationTypes[ConfigurationType.FeatureConfiguration].GetResolvedConfigurationObject(this);
        }

        private IAppConfiguration ResolveSettings()
        {
            return (IAppConfiguration)_configurationTypes[ConfigurationType.AppConfiguration].GetResolvedConfigurationObject(this);
        }

        private IServiceBusConfiguration ResolveServiceBus()
        {
            return (IServiceBusConfiguration)_configurationTypes[ConfigurationType.ServiceBusConfiguration].GetResolvedConfigurationObject(this);
        }

        private IPackageDeleteConfiguration ResolvePackageDelete()
        {
            return (IPackageDeleteConfiguration)_configurationTypes[ConfigurationType.PackageDeleteConfiguration].GetResolvedConfigurationObject(this);
        }

        private ICollection<string> GetAllSecretNames()
        {
            var secretNames = new HashSet<string>();
            foreach (var configurationDescription in _configurationTypes.Values)
            {
                secretNames.UnionWith(configurationDescription.GetSecretNames(this));
            }
            return secretNames;
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

        private class TelemetryConfiguration : ITelemetryConfiguration
        {
            public string AppInsightsInstrumentationKey { get; set; }
            public double AppInsightsSamplingPercentage { get; set; }
            public int AppInsightsHeartbeatIntervalSeconds { get; set; }
            [DefaultValue(null)]
            public string DeploymentLabel { get; set; }
        }
    }
}
