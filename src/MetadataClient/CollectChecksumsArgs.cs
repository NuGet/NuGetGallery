using System;
using PowerArgs;

namespace MetadataClient
{
    public class CollectChecksumsArgs : CatalogArgs
    {
        [ArgShortcut("chk")]
        [ArgDescription("The file to write the checksum data to")]
        public string ChecksumFile { get; set; }
    }
}