using System;
using PowerArgs;

namespace MetadataClient
{
    public class BlobStorageArgs
    {
        [ArgRequired]
        [ArgDescription("Storage Connection String")]
        public string StorageConnectionString { get; set; }
    }
}
