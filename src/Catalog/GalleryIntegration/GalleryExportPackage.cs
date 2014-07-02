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
        public List<JObject> Dependencies { get; set; }
        public List<string> TargetFrameworks { get; set; }
        public Uri GalleryDetailsUrl { get; set; }
        public Uri ReportAbuseUrl { get; set; }
        public Uri DownloadUrl { get; set; }
    }
}
