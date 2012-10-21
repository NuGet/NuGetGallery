using Microsoft.WindowsAzure.StorageClient;

namespace NuGetGallery
{
    public class CloudBlobContainerWrapper : ICloudBlobContainer
    {
        private readonly CloudBlobContainer _blobContainer;

        public CloudBlobContainerWrapper(CloudBlobContainer blobContainer)
        {
            _blobContainer = blobContainer;
        }

        public void CreateIfNotExist()
        {
            _blobContainer.CreateIfNotExist();
        }

        public void SetPermissions(BlobContainerPermissions permissions)
        {
            _blobContainer.SetPermissions(permissions);
        }

        public ICloudBlob GetBlobReference(string blobAddressUri)
        {
            return new CloudBlobWrapper(_blobContainer.GetBlobReference(blobAddressUri));
        }
    }
}