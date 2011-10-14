using Microsoft.WindowsAzure.StorageClient;

namespace NuGetGallery
{
    public interface ICloudBlobContainer
    {
        void CreateIfNotExist();
        void SetPermissions(BlobContainerPermissions permissions);
        ICloudBlob GetBlobReference(string blobAddressUri);
    }
}
