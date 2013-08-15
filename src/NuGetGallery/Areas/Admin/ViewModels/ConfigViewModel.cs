using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using Ninject.Planning.Bindings;
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

    public class ConfigViewModel
    {
        public IDictionary<string, Tuple<Type, object>> AppSettings { get; private set; }

        public IList<FeatureConfigViewModel> Features { get; private set; }

        public ConfigViewModel(IDictionary<string, Tuple<Type, object>> appSettings, IList<FeatureConfigViewModel> features)
        {
            AppSettings = appSettings;
            Features = features;
        }
    }
}