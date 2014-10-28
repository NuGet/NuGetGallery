using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace NuGetFeed
{
    public class ResolverStep : BuildStep
    {
        private readonly CollectorCursor _cursor;

        public ResolverStep(Config config)
            : this(config, new CollectorCursor(DateTime.MinValue))
        {

        }

        public ResolverStep(Config config, CollectorCursor cursor)
            : base(config, "PackageRegistrations")
        {
            _cursor = cursor;
        }

        protected override void RunCore()
        {
            RegistrationCatalogCollector collector = new RegistrationCatalogCollector(Config.PackageRegistrations.StorageFactory, 200);

            var indexUri = new Uri(Config.Catalog.BaseAddress + "index.json");
            var task = collector.Run(indexUri, _cursor, Config.Catalog.FileSystemEmulator);
            task.Wait();


            //ResolverCollector collector = new ResolverCollector(Config.PackageRegistrations.Storage, 1000);
            //collector.GalleryBaseAddress = Config.Gallery.BaseAddress.AbsoluteUri.TrimEnd('/');
            //collector.ContentBaseAddress = Config.Packages.BaseAddress.AbsoluteUri.TrimEnd('/');

            //var indexUri = new Uri(Config.Catalog.BaseAddress + "index.json");
            //var task = collector.Run(indexUri, _cursor, Config.Catalog.FileSystemEmulator);
            //task.Wait();
        }
    }
}
