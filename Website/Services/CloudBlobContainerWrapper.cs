using Microsoft.WindowsAzure.StorageClient;

namespace NuGetGallery
{
    public class CloudBlobContainerWrapper : ICloudBlobContainer
    {
        CloudBlobContainer blobContainer;

        public CloudBlobContainerWrapper(CloudBlobContainer blobContainer)
        {
            this.blobContainer = blobContainer;
        }

        public void CreateIfNotExist()
        {
            blobContainer.CreateIfNotExist();
        }

        public void SetPermissions(BlobContainerPermissions permissions)
        {
            blobContainer.SetPermissions(permissions);
        }

        public ICloudBlob GetBlobReference(string blobAddressUri)
        {
            return new CloudBlobWrapper(blobContainer.GetBlobReference(blobAddressUri));
        }
    }
}