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

        [ArgShortcut("base")]
        [ArgDescription("The base address to use for the URIs, defaults to http://api.nuget.org")]
        [DefaultValue("http://api.nuget.org")]
        public string BaseAddress { get; set; }

    }
}