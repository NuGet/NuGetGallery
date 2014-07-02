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
        private Uri _galleryBaseUrl;
        private Uri _downloadBaseUrl;
        public GalleryExportBatcher(int batchSize, CatalogWriter writer, Uri galleryBaseUrl, Uri downloadBaseUrl) : base(batchSize, writer)
        {
            _galleryBaseUrl = galleryBaseUrl;
            _downloadBaseUrl = downloadBaseUrl;
        }

        public Task Process(JObject package, string id, List<JObject> dependencies, List<string> targetFrameworks)
        {
            string version = package.Value<string>("NormalizedVersion").ToLowerInvariant();
            id = id.ToLowerInvariant();
            var export = new GalleryExportPackage
            {
                Package = package,
                Id = id,
                Dependencies = dependencies,
                TargetFrameworks = targetFrameworks
            };
            if (_galleryBaseUrl != null)
            {
                export.GalleryDetailsUrl = new Uri(_galleryBaseUrl, "packages/" + id + "/" + version);
                export.ReportAbuseUrl = new Uri(_galleryBaseUrl, "packages/" + id + "/" + version + "/ReportAbuse");
            }
            if (_downloadBaseUrl != null)
            {
                export.DownloadUrl = new Uri(_downloadBaseUrl, id + "/" + version + ".nupkg");
            }
            var item = new GalleryExportCatalogItem(export);
            return Add(item);
        }
    }
}
