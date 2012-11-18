using Microsoft.WindowsAzure.Storage.Blob;

namespace NuGetGallery
{
    public interface ICloudBlobContainer
    {
        void CreateIfNotExist();
        void SetPermissions(BlobContainerPermissions permissions);
        ISimpleCloudBlob GetBlobReference(string blobAddressUri);
    }
}