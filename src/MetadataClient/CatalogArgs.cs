using System;
using PowerArgs;

namespace MetadataClient
{

    public class CatalogArgs
    {
        private Uri _indexUrl;

        [ArgShortcut("c")]
        [ArgDescription("The folder containing the catalog")]
        public string CatalogFolder { get; set; }

        [ArgShortcut("cst")]
        [ArgDescription("Connection string to a storage account to hold the catalog")]
        public string CatalogStorage { get; set; }

        [ArgShortcut("cp")]
        [ArgDescription("The path within the storage account to place the catalog")]
        public string CatalogStoragePath { get; set; }

        [ArgShortcut("base")]
        [ArgDescription("The base address to use for URIs")]
        public Uri BaseAddress { get; set; }

        [ArgShortcut("i")]
        [ArgDescription("The Url for the index document")]
        public Uri IndexUrl
        {
            get { return _indexUrl ?? new Uri(BaseAddress, "index.json"); }
            set { _indexUrl = value; }
        }
    }
}