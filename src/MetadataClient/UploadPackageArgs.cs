using System;
using PowerArgs;

namespace MetadataClient
{
    public class UploadPackageArgs : BlobStorageArgs
    {
        [ArgRequired]
        [ArgDescription("Path to package to be uploaded to blob storage")]        
        public string Path { get; set; }

        [ArgDescription("Container name")]
        public string ContainerName { get; set; }

        [ArgDescription("Cache control for the blob to be uploaded")]
        public string CacheControl { get; set; }

        [ArgDescription("Content type for the blob to be uploaded")]
        public string ContentType { get; set; }
    }
}
