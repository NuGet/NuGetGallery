using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NuGet.Services.Configuration
{
    public interface ICustomConfigurationSection
    {
        void Resolve(string prefix, ConfigurationHub hub);
    }
}
