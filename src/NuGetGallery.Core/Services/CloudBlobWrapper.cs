using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.RetryPolicies;

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
            return _blob.DeleteIfExistsAsync();
        }

        public void DownloadToFile(string fileName)
        {
            using (Stream strm = File.OpenWrite(fileName))
            {
                _blob.DownloadToStream(strm);
            }
        }

        public Task DownloadToFileAsync(string path)
        {
            return _blob.DownloadToFileAsync(path, FileMode.Create);
        }

        public Task DownloadToStreamAsync(Stream target)
        {
            return DownloadToStreamAsync(target, accessCondition: null);
        }

        public Task DownloadToStreamAsync(Stream target, AccessCondition accessCondition)
        {
            var options = new BlobRequestOptions()
            {
                // The default retry policy treats a 304 as an error that requires a retry. We don't want that!
                RetryPolicy = new DontRetryOnNotModifiedPolicy(new LinearRetry())
            };

            return _blob.DownloadToStreamAsync(target, accessCondition, options, operationContext: null);
        }

        public Task<bool> ExistsAsync()
        {
            return _blob.ExistsAsync();
        }

        public Task SetPropertiesAsync()
        {
            return _blob.SetPropertiesAsync();
        }

        public Task UploadFromStreamAsync(Stream content)
        {
            return _blob.UploadFromStreamAsync(content);
        }

        public Task UploadFromFileAsync(string path)
        {
            return _blob.UploadFromFileAsync(path, FileMode.Open);
        }

        public void UploadFromFile(string path)
        {
            _blob.UploadFromFile(path, FileMode.Open);
        }

        public Task FetchAttributesAsync()
        {
            return _blob.FetchAttributesAsync();
        }

        // The default retry policy treats a 304 as an error that requires a retry. We don't want that!
        private class DontRetryOnNotModifiedPolicy : IRetryPolicy
        {
            private IRetryPolicy _innerPolicy;

            public DontRetryOnNotModifiedPolicy(IRetryPolicy policy)
            {
                _innerPolicy = policy;
            }

            public IRetryPolicy CreateInstance()
            {
                return new DontRetryOnNotModifiedPolicy(_innerPolicy.CreateInstance());
            }

            public bool ShouldRetry(int currentRetryCount, int statusCode, Exception lastException, out TimeSpan retryInterval, OperationContext operationContext)
            {
                if (statusCode == (int)HttpStatusCode.NotModified)
                {
                    retryInterval = TimeSpan.Zero;
                    return false;
                }
                else
                {
                    return _innerPolicy.ShouldRetry(currentRetryCount, statusCode, lastException, out retryInterval, operationContext);
                }
            }
        }
    }
}
