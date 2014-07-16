using System;
using PowerArgs;

namespace MetadataClient
{
    public class UpdateCatalogArgs : CollectChecksumsArgs
    {
        [ArgRequired]
        [ArgShortcut("db")]
        [ArgDescription("The connection string to the SQL Database containing Gallery Data")]
        public string SqlConnectionString { get; set; }
    }
}