using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog.GalleryIntegration
{
    public class GalleryExportPackage
    {
        public JObject Package { get; set; }
        public string Id { get; set; }
        public IList<JObject> Dependencies { get; set; }
        public IList<string> TargetFrameworks { get; set; }
    }
}
