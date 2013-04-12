using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Blob;

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

        public string Name
        {
            get { return _blob.Name; }
        }

        public DateTime LastModifiedUtc
        {
            get { return _blob.Properties.LastModified.HasValue ? _blob.Properties.LastModified.Value.UtcDateTime : DateTime.MinValue; }
        }

        public string ETag
        {
            get { return _blob.Properties.ETag; }
        }

        public Task DeleteIfExistsAsync()
        {
            return Task.Factory.FromAsync<bool>(_blob.BeginDeleteIfExists(null, null), _blob.EndDeleteIfExists);
        }

        public Task DownloadToStreamAsync(Stream target)
        {
            return Task.Factory.FromAsync(_blob.BeginDownloadToStream(target, null, null), _blob.EndDownloadToStream);
        }

        public Task<bool> ExistsAsync()
        {
            return Task.Factory.FromAsync<bool>(_blob.BeginExists(null, null), _blob.EndExists);
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
