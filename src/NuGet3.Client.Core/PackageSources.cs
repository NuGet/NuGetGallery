using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet3.Client.Core
{
    public class PackageSources
    {
        Dictionary<string, string> _sources = new Dictionary<string, string>();

        public PackageSources()
        {
        }

        public void Initialize()
        {
            JObject sources;
            try
            {
                sources = JObject.Parse(File.ReadAllText("sources.config.json"));
                foreach (var source in sources["packageSources"])
                {
                    JObject sourceObj = source as JObject;
                    _sources.Add((string)sourceObj["name"], (string)sourceObj["url"]);
                }
            }
            catch (FileNotFoundException)
            {
                _sources.Add("nuget.org", "http://preview.nuget.org/ver3-ctp1/intercept.json");
            }
        }

        public IEnumerable<PackageSource> Sources()
        {
            return _sources.Select(x => new PackageSource(x.Key, x.Value));
        }
    }
}
