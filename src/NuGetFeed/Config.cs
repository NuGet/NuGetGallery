using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetFeed
{
    public class Config
    {
        public ConfigPath Catalog { get; set; }
        public ConfigPath PackageRegistrations { get; set; }
        public ConfigPath Packages { get; set; }

        public ConfigPath Gallery { get; set; }

        /// <summary>
        /// Create a config for a new location
        /// </summary>
        public Config(string endpointAddress, string rootFolder)
        {
            Catalog = new ConfigPath(endpointAddress, rootFolder, "catalog");
            PackageRegistrations = new ConfigPath(endpointAddress, rootFolder, "packageregistrations");
            Packages = new ConfigPath(endpointAddress, rootFolder, "");
            Gallery = new ConfigPath(endpointAddress, rootFolder, "gallery");
        }
    }
}
