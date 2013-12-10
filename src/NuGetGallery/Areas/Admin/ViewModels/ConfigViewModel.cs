using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using Ninject.Planning.Bindings;
using NuGetGallery.Authentication.Providers;
using NuGetGallery.Configuration;

namespace NuGetGallery.Areas.Admin.ViewModels
{
    public class FeatureConfigViewModel
    {
        public PropertyInfo Property { get; private set; }

        public string Description { get; set; }

        public bool Enabled { get; set; }

        public FeatureConfigViewModel(PropertyInfo property, FeatureConfiguration config)
        {
            Property = property;

            var desca = property.GetCustomAttribute<DescriptionAttribute>();
            Description = (desca != null ? desca.Description : "");
            Enabled = (bool)property.GetValue(config);
        }
    }

    public class AuthConfigViewModel
    {
        public string Name { get; private set; }

        public IDictionary<string, string> Config { get; private set; }

        public AuthConfigViewModel(Authenticator provider)
        {
            Name = provider.Name;
            Config = provider.BaseConfig.GetConfigValues();
        }
    }

    public class ConfigViewModel
    {
        public IDictionary<string, Tuple<Type, object>> AppSettings { get; private set; }

        public IList<FeatureConfigViewModel> Features { get; private set; }

        public IList<AuthConfigViewModel> AuthProviders { get; private set; }

        public ConfigViewModel(IDictionary<string, Tuple<Type, object>> appSettings, IList<FeatureConfigViewModel> features, IEnumerable<Authenticator> authProviders)
        {
            AppSettings = appSettings;
            Features = features;
            AuthProviders = authProviders.Select(p => new AuthConfigViewModel(p)).ToList();
        }
    }
}