using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Ninject.Planning.Bindings;

namespace NuGetGallery.Areas.Admin.ViewModels
{
    public class ConfigViewModel
    {
        public Dictionary<string, object> AppSettings { get; private set; }
        
        public ConfigViewModel(Dictionary<string, object> appSettings)
        {
            AppSettings = appSettings;
        }
    }
}
