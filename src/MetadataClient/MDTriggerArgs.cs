using System;
using PowerArgs;
using System.Data.SqlClient;

namespace MetadataClient
{
    public class MDTriggerArgs : BlobStorageArgs
    {
        [ArgRequired]
        [ArgDescription("DB Connection String")]
        public string DBConnectionString { get; set; }

        [ArgDescription("Container name")]
        public string ContainerName { get; set; }
    }
}
