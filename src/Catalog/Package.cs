using Catalog.Persistence;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VDS.RDF;

namespace Catalog
{
    public class Package
    {
        IGraph _graph;

        public Uri Address
        {
            get;
            set;
        }

        public Package(IGraph graph)
        {
            _graph = graph;
        }

        public async Task SaveTo(Storage storage)
        {
            JToken frame;
            using (JsonReader jsonReader = new JsonTextReader(new StreamReader(Utils.GetResourceStream("context.PackageFrame.json"))))
            {
                frame = JObject.Load(jsonReader);
                frame["@type"] = "http://nuget.org/schema#Package";
            }

            string content = Utils.CreateJson(_graph, frame);

            await storage.Save("application/json", Address, content);
        }
    }
}
