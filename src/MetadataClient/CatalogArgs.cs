using System;
using PowerArgs;

namespace MetadataClient
{

    public class CatalogArgs
    {
        private Uri _indexUrl;

        [ArgRequired]
        [ArgShortcut("c")]
        [ArgDescription("The folder containing the catalog")]
        public string CatalogFolder
        { get; set; }

        [ArgRequired]
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