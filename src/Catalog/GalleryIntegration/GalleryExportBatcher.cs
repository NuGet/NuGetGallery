using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Maintenance;
using NuGet.Services.Metadata.Catalog.Persistence;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog.GalleryIntegration
{
    public class GalleryExportBatcher : CatalogBatcher
    {
        public GalleryExportBatcher(int batchSize, CatalogWriter writer) : base(batchSize, writer) { }

        public Task Process(JObject package, string registration, List<JObject> dependencies, List<string> targetFrameworks)
        {
            var export = new GalleryExportPackage
            {
                Package = package,
                Id = registration,
                Dependencies = dependencies,
                TargetFrameworks = targetFrameworks
            };
            var item = new GalleryExportCatalogItem(export);
            return Add(item);
        }
    }
}
