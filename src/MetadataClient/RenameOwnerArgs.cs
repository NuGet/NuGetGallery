using System;
using PowerArgs;

namespace MetadataClient
{
    public class RenameOwnerArgs : BlobStorageArgs
    {
        [ArgRequired]
        [ArgDescription("Old owner name")]
        public string OldName { get; set; }

        [ArgRequired]
        [ArgDescription("New owner name")]
        public string NewName { get; set; }
    }
}
