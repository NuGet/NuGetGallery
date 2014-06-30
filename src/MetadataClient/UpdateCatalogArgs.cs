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
        [ArgShortcut("c")]
        [ArgDescription("The root URL to the catalog, EXCLUDING index.json segment")]
        public Uri CatalogRootUrl { get; set; }

        [ArgShortcut("base")]
        [ArgDescription("The base address to use for the URIs, defaults to http://api.nuget.org")]
        [DefaultValue("http://api.nuget.org")]
        public Uri BaseAddress { get; set; }

        [ArgRequired]
        [ArgShortcut("d")]
        [ArgDescription("The directory containing the catalog")]
        public string CatalogFolder { get; set; }
    }
}