using System.IO;

namespace NuGet.Services.Metadata.Catalog.Persistence
{
    public class StreamStorageContent : StorageContent
    {
        public StreamStorageContent(Stream content, string contentType = "")
        {
            Content = content;
            ContentType = contentType;
        }

        public Stream Content
        {
            get;
            set;
        }

        public override Stream GetContentStream()
        {
            return Content;
        }
    }
}
