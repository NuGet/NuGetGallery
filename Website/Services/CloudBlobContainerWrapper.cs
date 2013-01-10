using Microsoft.WindowsAzure.Storage.Blob;

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
            _blobContainer.CreateIfNotExists();
        }

        public void SetPermissions(BlobContainerPermissions permissions)
        {
            _blobContainer.SetPermissions(permissions);
        }

        public ISimpleCloudBlob GetBlobReference(string blobName)
        {
            return new CloudBlobWrapper(_blobContainer.GetBlockBlobReference(blobName));
        }
    }
}