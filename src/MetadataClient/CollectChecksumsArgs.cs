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
        public Uri IndexUrl { get; set; }

        [ArgShortcut("chk")]
        [ArgDescription("The file to write the checksum data to")]
        public string ChecksumFile { get; set; }
    }
}