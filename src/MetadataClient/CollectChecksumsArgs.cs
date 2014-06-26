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

        [ArgShortcut("base")]
        [ArgDescription("The base address to use for the URIs, defaults to http://api.nuget.org")]
        [DefaultValue("http://api.nuget.org")]
        public Uri BaseAddress { get; set; }

        [ArgRequired]
        [ArgShortcut("i")]
        [ArgDescription("The Url for the index document")]
        public Uri IndexUrl { get; set; }

        [ArgRequired]
        [ArgShortcut("d")]
        [ArgDescription("The file to write the checksum data to")]
        public string DestinationFile { get; set; }
    }
}