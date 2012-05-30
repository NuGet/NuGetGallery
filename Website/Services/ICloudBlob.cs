using System;
using System.IO;
using Microsoft.WindowsAzure.StorageClient;

namespace NuGetGallery
{
    public interface ICloudBlob
    {
        BlobProperties Properties { get; }
        Uri Uri { get; }

        void DeleteIfExists();
        void DownloadToStream(Stream target);
        bool Exists();
        void SetProperties();
        void UploadFromStream(Stream packageFile);
    }
}
