using System.Collections.Generic;
using Newtonsoft.Json;
using NuGet.Services.Metadata.Catalog.Helpers;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    /// <summary>
    /// The data to be passed to <see cref="PackageValidator.ValidateAsync(PackageValidatorContext, CollectorHttpClient, System.Threading.CancellationToken)"/>.
    /// </summary>
    public class PackageValidatorContext
    {
        /// <summary>
        /// This should be incremented every time the structure of this class changes.
        /// </summary>
        public const int Version = 1;
        
        /// <summary>
        /// The package to run validations on.
        /// </summary>
        public FeedPackageIdentity Package { get; private set; }

        /// <summary>
        /// The catalog entries that initiated this request to run validations.
        /// </summary>
        public IEnumerable<CatalogIndexEntry> CatalogEntries { get; private set; }

        [JsonConstructor]
        public PackageValidatorContext(FeedPackageIdentity package, IEnumerable<CatalogIndexEntry> catalogEntries)
        {
            Package = package;
            CatalogEntries = catalogEntries;
        }
    }
}