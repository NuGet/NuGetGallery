using System;
using System.IO;
using Microsoft.WindowsAzure.StorageClient;

namespace NuGetGallery
{
    public class CloudBlobWrapper : ICloudBlob
    {
        private readonly CloudBlob _blob;

        public CloudBlobWrapper(CloudBlob blob)
        {
            _blob = blob;
        }

        public BlobProperties Properties
        {
            get { return _blob.Properties; }
        }

        public Uri Uri
        {
            get { return _blob.Uri; }
        }

        public void DeleteIfExists()
        {
            _blob.DeleteIfExists();
        }

        public void DownloadToStream(Stream target)
        {
            try
            {
                _blob.DownloadToStream(target);
            }
            catch (StorageClientException ex)
            {
                throw new TestableStorageClientException(ex);
            }
        }

        public bool Exists()
        {
            try
            {
                _blob.FetchAttributes();
                return true;
            }
            catch (StorageClientException e)
            {
                if (e.ErrorCode == StorageErrorCode.ResourceNotFound)
                {
                    return false;
                }
                else
                {
                    throw;
                }
            }
        }

        public void SetProperties()
        {
            _blob.SetProperties();
        }

        public void UploadFromStream(Stream packageFile)
        {
            _blob.UploadFromStream(packageFile);
        }
    }
}