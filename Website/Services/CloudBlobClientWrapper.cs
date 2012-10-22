using Microsoft.WindowsAzure.StorageClient;

namespace NuGetGallery
{
    public class CloudBlobClientWrapper : ICloudBlobClient
    {
        private readonly CloudBlobClient _blobClient;

        public CloudBlobClientWrapper(CloudBlobClient blobClient)
        {
            _blobClient = blobClient;
        }

        public ICloudBlobContainer GetContainerReference(string containerAddress)
        {
            return new CloudBlobContainerWrapper(_blobClient.GetContainerReference(containerAddress));
        }
    }
}