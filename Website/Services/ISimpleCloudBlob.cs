using System;
using System.IO;
using Microsoft.WindowsAzure.Storage.Blob;

namespace NuGetGallery
{
    public interface ISimpleCloudBlob
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
