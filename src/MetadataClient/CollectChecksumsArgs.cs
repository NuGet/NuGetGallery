using System;
using PowerArgs;

namespace MetadataClient
{
    public class CollectChecksumsArgs
    {
        [ArgRequired]
        [ArgShortcut("c")]
        [ArgDescription("The folder containing the catalog")]
        public string CatalogFolder { get; set; }

        [ArgRequired]
        [ArgShortcut("base")]
        [ArgDescription("The base address to use for URIs")]
        public Uri BaseAddress { get; set; }

        [ArgShortcut("i")]
        [ArgDescription("The Url for the index document")]
        [DefaultValue("http://api.nuget.org/catalogs/primary/index.json")]
        public Uri IndexUrl { get; set; }

        [ArgRequired]
        [ArgShortcut("d")]
        [ArgDescription("The file to write the checksum data to")]
        public string DestinationFile { get; set; }
    }
}