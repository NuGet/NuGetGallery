using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Ninject;
using NuGetGallery.Configuration;

namespace NuGetGallery.Views
{
    public abstract class NuGetViewBase : WebViewPage
    {
        private ConfigurationService _config;
        public ConfigurationService Config
        {
            get { return _config ?? (_config = Container.Kernel.TryGet<ConfigurationService>()); }
            set { _config = value; }
        }
    }

    public abstract class NuGetViewBase<T> : WebViewPage<T>
    {
        private ConfigurationService _config;
        public ConfigurationService Config
        {
            get { return _config ?? (_config = Container.Kernel.TryGet<ConfigurationService>()); }
            set { _config = value; }
        }
    }
}