using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
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
            return Task.Factory.FromAsync<bool>(
                (cb, state) => _blob.BeginDeleteIfExists(cb, state), 
                ar => _blob.EndDeleteIfExists(ar),
                state: null);
        }

        public Task DownloadToStreamAsync(Stream target)
        {
            return DownloadToStreamAsync(target, accessCondition: null);
        }

        public Task DownloadToStreamAsync(Stream target, AccessCondition accessCondition)
        {
            // Note: Overloads of FromAsync that take an AsyncCallback and State to pass through are more efficient:
            //  http://blogs.msdn.com/b/pfxteam/archive/2009/06/09/9716439.aspx
            return Task.Factory.FromAsync(
                (cb, state) => _blob.BeginDownloadToStream(
                    target, 
                    accessCondition,
                    options: null,
                    operationContext: null,
                    callback: cb, 
                    state: state), 
                ar => _blob.EndDownloadToStream(ar),
                state: null);
        }

        public Task<bool> ExistsAsync()
        {
            return Task.Factory.FromAsync(
                (cb, state) => _blob.BeginExists(cb, state), 
                ar => _blob.EndExists(ar),
                state: null);
        }

        public Task SetPropertiesAsync()
        {
            return Task.Factory.FromAsync(
                (cb, state) => _blob.BeginSetProperties(cb, state), 
                ar => _blob.EndSetProperties(ar),
                state: null);
        }

        public Task UploadFromStreamAsync(Stream packageFile)
        {
            return Task.Factory.FromAsync(
                (cb, state) => _blob.BeginUploadFromStream(packageFile, cb, state), 
                ar => _blob.EndUploadFromStream(ar),
                state: null);
        }
    }
}
