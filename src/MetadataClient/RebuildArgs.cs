using System;
using PowerArgs;

namespace MetadataClient
{
    public class RebuildArgs
    {
        [ArgRequired]
        [ArgShortcut("db")]
        [ArgDescription("The connection string to the SQL Database containing Gallery Data")]
        public string SqlConnectionString { get; set; }

        [ArgRequired]
        [ArgShortcut("d")]
        [ArgDescription("The destination folder in which to write the catalog")]
        public string DestinationFolder { get; set; }

        [ArgRequired]
        [ArgShortcut("base")]
        [ArgDescription("The base address to use for URIs")]
        public Uri BaseAddress { get; set; }

    }
}