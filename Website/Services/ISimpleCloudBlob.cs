using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Blob;

namespace NuGetGallery
{
    public interface ISimpleCloudBlob
    {
        BlobProperties Properties { get; }
        Uri Uri { get; }

        Task DeleteIfExistsAsync();
        Task DownloadToStreamAsync(Stream target);

        Task<bool> ExistsAsync();
        Task SetPropertiesAsync();
        Task UploadFromStreamAsync(Stream packageFile);
    }
}