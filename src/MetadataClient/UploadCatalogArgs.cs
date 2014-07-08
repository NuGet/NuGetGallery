using System;
using PowerArgs;

namespace MetadataClient
{
    public class UploadCatalogArgs
    {
        [ArgRequired]
        [ArgShortcut("c")]
        [ArgDescription("The folder containing the catalog")]
        public string CatalogFolder { get; set; }

        [ArgRequired]
        [ArgShortcut("st")]
        [ArgDescription("The connection string to the storage account which will contain the catalog")]
        public string CatalogStorage { get; set; }

        [ArgRequired]
        [ArgShortcut("path")]
        [ArgDescription("The path to the desired root of the catalog on storage")]
        public string CatalogPath { get; set; }
    }
}