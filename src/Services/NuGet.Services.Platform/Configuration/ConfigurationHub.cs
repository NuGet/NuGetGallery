using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.ServiceRuntime;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Collections.Concurrent;
using System.Globalization;
using NuGet.Services.ServiceModel;

namespace NuGet.Services.Configuration
{
    public class ConfigurationHub
    {
        private Func<string, string> _getSettingThunk;
        private static readonly Regex NameMatch = new Regex("(?<shortname>.*)Configuration", RegexOptions.IgnoreCase);

        private ConcurrentDictionary<Type, object> _configCache = new ConcurrentDictionary<Type,object>();

        public StorageConfiguration Storage { get { return GetSection<StorageConfiguration>(); } }
        public SqlConfiguration Sql { get { return GetSection<SqlConfiguration>(); } }
        public HttpConfiguration Http { get { return GetSection<HttpConfiguration>(); } }

        public ConfigurationHub(ServiceHost host)
        {
            _getSettingThunk = host.GetConfigurationSetting;
        }

        public ConfigurationHub(IDictionary<string, string> settings)
        {
            _getSettingThunk = key =>
            {
                string val;
                if (!settings.TryGetValue(key, out val))
                {
                    return null;
                }
                return val;
            };
        }

        public ConfigurationHub(Func<string, string> getSettingThunk)
        {
            _getSettingThunk = getSettingThunk;
        }

        public virtual T GetSection<T>()
            where T : new()
        {
            return (T)_configCache.GetOrAdd(typeof(T), _ =>
            {
                string name = typeof(T).Name;
                var match = NameMatch.Match(name);
                if (match.Success)
                {
                    name = match.Groups["shortname"].Value;
                }

                var attr = typeof(T).GetCustomAttribute<ConfigurationSectionAttribute>();
                if (attr != null)
                {
                    name = attr.Name;
                }

                return ResolveConfiguration(name + ".", new T());
            });
        }

        public virtual string GetSetting(string fullName)
        {
            return _getSettingThunk(fullName);
        }

        protected virtual IEnumerable<PropertyDescriptor> GetConfigProperties<T>(T instance)
        {
            return TypeDescriptor.GetProperties(instance).Cast<PropertyDescriptor>().Where(p => !p.IsReadOnly);
        }

        protected virtual T ResolveConfiguration<T>(string prefix, T instance)
        {
            // Check if the section can resolve itself
            ICustomConfigurationSection custom = instance as ICustomConfigurationSection;
            if (custom != null)
            {
                custom.Resolve(prefix, this);
                return instance;
            }

            // Iterate over the properties
            foreach (var property in GetConfigProperties<T>(instance))
            {
                // Try to get a config setting value
                string baseName = String.IsNullOrEmpty(property.DisplayName) ? property.Name : property.DisplayName;
                string settingName = prefix + baseName;

                string value = GetSetting(settingName);

                if (String.IsNullOrEmpty(value))
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

                if (!String.IsNullOrEmpty(value))
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
                    throw new ConfigurationException(String.Format(CultureInfo.InvariantCulture, "Missing required configuration setting: '{0}'", settingName));
                }
            }
            return instance;
        }
    }
}
