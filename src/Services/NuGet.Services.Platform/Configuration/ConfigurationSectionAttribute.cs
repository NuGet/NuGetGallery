using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NuGet.Services.Configuration
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class ConfigurationSectionAttribute : Attribute
    {
        public string Name { get; private set; }
        public ConfigurationSectionAttribute(string name)
        {
            Name = name;
        }
    }
}
