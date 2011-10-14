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

        public Uri Uri
        {
            get { return blob.Uri; }
        }

        public void DeleteIfExists()
        {
            blob.DeleteIfExists();
        }

        public void UploadFromStream(Stream source)
        {
            blob.UploadFromStream(source);
        }
    }
}