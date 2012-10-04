using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.StorageClient;

namespace NuGetGallery
{
    public interface ICloudBlob
    {
        BlobProperties Properties { get; }
        Uri Uri { get; }

        void DeleteIfExists();
        void DownloadToStream(Stream target);
        Task DownloadToStreamAsync(Stream target);

        bool Exists();
        void SetProperties();
        void UploadFromStream(Stream packageFile);
    }
}
