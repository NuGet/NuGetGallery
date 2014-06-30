using System;
using PowerArgs;

namespace MetadataClient
{
    public class UpdateCatalogArgs
    {
        [ArgRequired]
        [ArgShortcut("db")]
        [ArgDescription("The connection string to the SQL Database containing Gallery Data")]
        public string SqlConnectionString { get; set; }

        [ArgRequired]
        [ArgShortcut("base")]
        [ArgDescription("The base address to use with the folder specified in -CatalogFolder")]
        public Uri BaseAddress { get; set; }

        [ArgRequired]
        [ArgShortcut("d")]
        [ArgDescription("The directory containing the catalog")]
        public string CatalogFolder { get; set; }
    }
}