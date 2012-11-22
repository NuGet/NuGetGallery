using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Shared.Protocol;

namespace NuGetGallery
{
    public class CloudBlobWrapper : ISimpleCloudBlob
    {
        private readonly ICloudBlob _blob;

        public CloudBlobWrapper(ICloudBlob blob)
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

        public Task DeleteIfExistsAsync()
        {
            return Task.Factory.FromAsync<bool>(_blob.BeginDeleteIfExists(null, null), _blob.EndDeleteIfExists);
        }

        public Task DownloadToStreamAsync(Stream target)
        {
            try
            {
                return Task.Factory.FromAsync(_blob.BeginDownloadToStream(target, null, null), _blob.EndDownloadToStream);
            }
            catch (StorageException ex)
            {
                throw new TestableStorageClientException(ex);
            }
        }

        public async Task<bool> ExistsAsync()
        {
            try
            {
                await Task.Factory.FromAsync(_blob.BeginFetchAttributes(null, null), _blob.EndFetchAttributes);
                return true;
            }
            catch (StorageException e)
            {
                if (e.RequestInformation.ExtendedErrorInformation.ErrorCode == StorageErrorCodeStrings.ResourceNotFound)
                {
                    return false;
                }
                else
                {
                    throw;
                }
            }
        }

        public Task SetPropertiesAsync()
        {
            return Task.Factory.FromAsync(_blob.BeginSetProperties(null, null), _blob.EndSetProperties);
        }

        public Task UploadFromStreamAsync(Stream packageFile)
        {
            return Task.Factory.FromAsync(_blob.BeginUploadFromStream(packageFile, null, null), _blob.EndUploadFromStream);
        }
    }
}