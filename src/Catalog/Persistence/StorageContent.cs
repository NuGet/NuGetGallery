using System.IO;

namespace NuGet.Services.Metadata.Catalog.Persistence
{
    public abstract class StorageContent
    {
        public string ContentType
        {
            get;
            set;
        }

        public string CacheControl
        {
            get;
            set;
        }

        public abstract Stream GetContentStream();
    }
}
