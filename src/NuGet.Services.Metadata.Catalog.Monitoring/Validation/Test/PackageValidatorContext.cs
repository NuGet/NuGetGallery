using System.Collections.Generic;
using Newtonsoft.Json;
using NuGet.Services.Metadata.Catalog.Helpers;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    public class PackageValidatorContext
    {
        public FeedPackageIdentity Package { get; private set; }

        public IEnumerable<CatalogIndexEntry> CatalogEntries { get; private set; }

        [JsonConstructor]
        public PackageValidatorContext(FeedPackageIdentity package, IEnumerable<CatalogIndexEntry> catalogEntries)
        {
            Package = package;
            CatalogEntries = catalogEntries;
        }
    }
}