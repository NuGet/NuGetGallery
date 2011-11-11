using System;
using System.IO;

namespace NuGetGallery
{
    public interface ICloudBlob
    {
        Uri Uri { get; }

        void DeleteIfExists();
        void DownloadToStream(Stream target);
        void UploadFromStream(Stream packageFile);
    }
}
