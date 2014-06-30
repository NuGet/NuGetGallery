using PowerArgs;

namespace MetadataClient
{
    public class ReadChecksumArgs
    {

        [ArgRequired]
        [ArgShortcut("f")]
        [ArgDescription("The file containing the checksums")]
        public string ChecksumFile { get; set; }

        [ArgRequired]
        [ArgShortcut("k")]
        [ArgDescription("The package key to look up")]
        public int PackageKey { get; set; }
    }
}