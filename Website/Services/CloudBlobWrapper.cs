using System;
using System.IO;
using Microsoft.WindowsAzure.StorageClient;

namespace NuGetGallery
{
    public class CloudBlobWrapper : ICloudBlob
    {
        CloudBlob blob;

        public CloudBlobWrapper(CloudBlob blob)
        {
            this.blob = blob;
        }

        public BlobProperties Properties 
        {
            get { return blob.Properties; }
        }

        public Uri Uri
        {
            get { return blob.Uri; }
        }

        public void DeleteIfExists()
        {
            blob.DeleteIfExists();
        }

        public void DownloadToStream(Stream target)
        {
            try
            {
                blob.DownloadToStream(target);
            }
            catch (StorageClientException ex)
            {
                throw new TestableStorageClientException(ex);
            }
        }

        public void SetProperties()
        {
            blob.SetProperties();
        }

        public void UploadFromStream(Stream packageFile)
        {
            blob.UploadFromStream(packageFile);
        }
    }
}