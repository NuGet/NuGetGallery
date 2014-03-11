using System;
using PowerArgs;

namespace MetadataClient
{
    public class MakeMetadataArgs : BlobStorageArgs
    {
        [ArgDescription("Received container name")]
        public string ReceivedContainer { get; set; }

        [ArgDescription("Publish container name")]
        public string PublishContainer { get; set; }
    }
}
