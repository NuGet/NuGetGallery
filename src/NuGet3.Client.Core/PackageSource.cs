using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet3.Client.Core
{
    public class PackageSource
    {
        public string Name { get; private set; }
        public string Url { get; private set; }

        public PackageSource(string name, string uri)
        {
            Name = name;
            Url = uri;
        }

        public void Initialize()
        {
        }
    }
}
