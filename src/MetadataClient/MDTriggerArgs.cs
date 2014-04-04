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

        [ArgShortcut("c")]
        [ArgDescription("Container name")]
        public string ContainerName { get; set; }

        [ArgDescription("DumpToCloud")]
        public bool DumpToCloud { get; set; }
    }
}
