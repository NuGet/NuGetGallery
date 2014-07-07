using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Maintenance;
using NuGet.Services.Metadata.Catalog.Persistence;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace NuGet.Services.Metadata.Catalog.GalleryIntegration
{
    public class GalleryExportBatcher : CatalogBatcher
    {
        public GalleryExportBatcher(int batchSize, CatalogWriter writer) : base(batchSize, writer)
        {
        }

        public Task Process(GalleryExportPackage package)
        {
            var item = new GalleryExportCatalogItem(package);
            return Add(item);
        }
    }
}
