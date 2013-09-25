using Microsoft.WindowsAzure.Storage.Blob;

namespace NuGetGallery
{
    public interface ICloudBlobClient
    {
        CloudBlobContainer GetContainerReference(string containerAddress);
    }
}