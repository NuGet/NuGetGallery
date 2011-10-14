using Microsoft.WindowsAzure.StorageClient;

namespace NuGetGallery
{
    public class CloudBlobClientWrapper : ICloudBlobClient
    {
        CloudBlobClient blobClient;

        public CloudBlobClientWrapper(CloudBlobClient blobClient)
        {
            this.blobClient = blobClient;
        }

        public ICloudBlobContainer GetContainerReference(string containerAddress)
        {
            return new CloudBlobContainerWrapper(blobClient.GetContainerReference(containerAddress));
        }
    }
}