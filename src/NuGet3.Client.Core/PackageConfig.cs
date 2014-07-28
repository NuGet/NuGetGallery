using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet3.Client
{
    public class PackageConfig
    {
        // TODO: this is awful--PCL file I/O is needlessly hard.
        public static JObject Load(string json)
        {
            return JObject.Parse(json);
        }
    }
}
