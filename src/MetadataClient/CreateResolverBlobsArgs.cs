using System;
using PowerArgs;

namespace MetadataClient
{
    public class CreateResolverBlobsArgs : CatalogArgs
    {
        [ArgRequired]
        [ArgShortcut("r")]
        [ArgDescription("The folder to write resolver blobs to")]
        public string ResolverFolder { get; set; }

        [ArgRequired]
        [ArgShortcut("rbase")]
        [ArgDescription("The base address for resolver blobs")]
        public string ResolverBase { get; set; }
    }
}